#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FamilyInstance = Autodesk.Revit.DB.FamilyInstance;
using Wall = Autodesk.Revit.DB.Wall;

#endregion

namespace SUN
{
    [Transaction(TransactionMode.Manual)]
    public class SunHourAnalysis : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            View3D activeView = doc.ActiveView as View3D;
            SunAndShadowSettings sunSettings = doc.ActiveView.SunAndShadowSettings;
            int minutes = 0;
            XYZ oddalone = null;
            List<XYZ> oddalonelista = new List<XYZ>();
            double numberOfFrames = sunSettings.NumberOfFrames;
            List<double> listOfAltitudes = new List<double>();
            List<double> listOfAzimuth = new List<double>();
            List<XYZ> sunVectors = new List<XYZ>();
            TaskDialog.Show("dsa", numberOfFrames.ToString());
            for (double i = 1; i <= numberOfFrames; i++)
            {
                double altitude = sunSettings.GetFrameAltitude(i);
                double azimuth = sunSettings.GetFrameAzimuth(i);
                listOfAltitudes.Add(altitude);
                listOfAzimuth.Add(azimuth);
                XYZ sunVector = sunExtensions.GetSunVector(altitude, azimuth);
                sunVectors.Add(sunVector);
            }
            string targetFamilyName = "MAMSunTarget";
            Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, new FamilyInstanceSelectionFilter(targetFamilyName),
                "Wybierz instancję rodziny '" + targetFamilyName + "'");
            Element selectedElement = doc.GetElement(pickedRef);
            Location loc = selectedElement.Location;
            LocationPoint locP = loc as LocationPoint;
            XYZ locationPPoint = locP.Point;


            using (Transaction trans = new Transaction(doc, "dada"))
            {
                trans.Start();

                FamilyInstance familyInstance = selectedElement as FamilyInstance;
                XYZ normal = FamilyInstanceNormal.GetFamilyNormal(familyInstance).Normalize();
                double odsunieciePunktuwFeetach = selectedElement.LookupParameter("od").AsDouble();
                XYZ analysisPoint = locationPPoint.MoveAlongVector(normal, -odsunieciePunktuwFeetach);

                foreach (XYZ sunVector in sunVectors)
                {
                    XYZ zero = XYZ.Zero;
                    oddalone = zero.MoveAlongVector(sunVector, 500);
                    oddalonelista.Add(oddalone);

                    if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, oddalone))
                    {
                        minutes++;
                    }

                }
                GetIntersections WI = new GetIntersections();
                List<GetIntersections> wynik = WI.GetIntersection(doc, activeView, oddalonelista, analysisPoint);

                // Połącz wszystkie punkty w jedną listę (zarówno FirstPoint, jak i LastPoint)
                List<XYZ> allPoints = wynik.SelectMany(w => new List<XYZ> { w.FirstPoint, w.LastPoint }).ToList();

                // Sortowanie punktów zgodnie z ruchem słońca (od wschodu do zachodu)
                allPoints.Sort((a, b) =>
                {
                    double angleA = Math.Atan2(a.Y - analysisPoint.Y, a.X - analysisPoint.X);
                    double angleB = Math.Atan2(b.Y - analysisPoint.Y, b.X - analysisPoint.X);

                    return angleB.CompareTo(angleA); // 🔄 Odwracamy kolejność
                });

                // Numerowanie punktów w kolejności słońca (1, 2, 3, ...)
                for (int i = 0; i < allPoints.Count; i++)
                {
                    XYZ textPosition = allPoints[i] + new XYZ(0, 0, 0.3); // Podniesienie numeracji dla lepszej widoczności
                    Text3DLabel.CreateText3D(doc, textPosition, (i + 1).ToString());
                }



                foreach (XYZ point in allPoints)
                {
                    point.Visualize(doc);
                    XYZ planeNormal = analysisPoint.CrossProduct(point);
                    SketchPlane sketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(planeNormal, analysisPoint));
                    Curve line = Line.CreateBound(analysisPoint, point) as Curve;
                    doc.Create.NewModelCurve(line, sketchPlane);

                }


                //1 case 7H 17H are covered
                // Sprawdzamy, czy pierwszy i ostatni promień są zasłonięte
                CreateTrianglesForFullySunlitPointsClass.CreateTrianglesForSunlitPoints(uiapp, doc, analysisPoint, wynik);







                int hours = minutes / 60;
                int remainingMinutes = minutes % 60;
                TaskDialog.Show("Sun Analysis Complete", $"Total Sun Hours at Point: {hours} hours and {remainingMinutes} minutes");

                Parameter parameter = selectedElement.LookupParameter("Nasłonecznienie");
                parameter.Set(hours + " h " + remainingMinutes + " min");

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }
}



