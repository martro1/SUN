#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SunHourAnalysisKopia : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View3D activeView = doc.ActiveView as View3D;
            SunAndShadowSettings sunSettings = doc.ActiveView.SunAndShadowSettings;

            int minutes = 0;
            List<XYZ> oddalonelista = new List<XYZ>();
            List<XYZ> sunVectors = new List<XYZ>();
            double numberOfFrames = sunSettings.NumberOfFrames;

            for (double i = 1; i <= numberOfFrames; i++)
            {
                double altitude = sunSettings.GetFrameAltitude(i);
                double azimuth = sunSettings.GetFrameAzimuth(i);
                XYZ sunVector = sunExtensions.GetSunVector(altitude, azimuth);
                sunVectors.Add(sunVector);
            }

            string targetFamilyName = "MAMSunTarget";
            Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, new FamilyInstanceSelectionFilter(targetFamilyName),
                "Wybierz instancj� rodziny '" + targetFamilyName + "'");
            Element selectedElement = doc.GetElement(pickedRef);
            LocationPoint locP = selectedElement.Location as LocationPoint;
            XYZ locationPPoint = locP.Point;


            using (Transaction trans = new Transaction(doc, "Analiza nas�onecznienia"))
            {
                trans.Start();

                FamilyInstance familyInstance = selectedElement as FamilyInstance;
                XYZ normal = FamilyInstanceNormal.GetFamilyNormal(familyInstance).Normalize();
                double odsunieciePunktuwFeetach = selectedElement.LookupParameter("od").AsDouble();
                XYZ analysisPoint = locationPPoint.MoveAlongVector(normal, -odsunieciePunktuwFeetach);

                foreach (XYZ sunVector in sunVectors)
                {
                    XYZ oddalone = XYZ.Zero.MoveAlongVector(sunVector, 500);
                    oddalonelista.Add(oddalone);
                    if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, oddalone))
                    {
                        minutes++;
                    }
                }

                GetIntersections WI = new GetIntersections();
                List<GetIntersections> wynik = WI.GetIntersection(doc, activeView, oddalonelista, analysisPoint,normal);

                if (wynik.Count < 2)
                {
                    TaskDialog.Show("B��d", "Za ma�o przeci�� do analizy.");
                    trans.RollBack();
                    return Result.Failed;
                }

                XYZ first = analysisPoint.MoveAlongVector(oddalonelista.First(), analysisPoint.DistanceTo(wynik.First().LastPoint));
                XYZ last = analysisPoint.MoveAlongVector(oddalonelista.Last(), analysisPoint.DistanceTo(wynik.Last().FirstPoint));

                bool firstBlocked = !sunExtensions.IsPointExposedToSun(doc, analysisPoint, first);
                bool lastBlocked = !sunExtensions.IsPointExposedToSun(doc, analysisPoint, last);

                if (firstBlocked && lastBlocked)
                {
                    TaskDialog.Show("Info", "Oba wektory (7:00 i 17:00) s� zas�oni�te.");
                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                    }
                }
                else if (firstBlocked && !lastBlocked)
                {
                    TaskDialog.Show("Info", "Wektor 7:00 jest zas�oni�ty, 17:00 ods�oni�ty.");
                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                    }
                    XYZ godzina17 = oddalonelista.Last();
                    XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                }
                else if (!firstBlocked && lastBlocked)
                {
                    TaskDialog.Show("Info", "Wektor 7:00 jest ods�oni�ty, 17:00 zas�oni�ty.");
                    XYZ godzina7 = oddalonelista.First();
                    XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);

                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                    }
                }
                else
                {
                    TaskDialog.Show("Info", "Oba wektory (7:00 i 17:00) s� ods�oni�te.");
                    XYZ godzina7 = oddalonelista.First();
                    XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);

                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                    }

                    XYZ godzina17 = oddalonelista.Last();
                    XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                }

                int hours = minutes / 60;
                int remainingMinutes = minutes % 60;
                TaskDialog.Show("Sun Analysis Complete", $"Total Sun Hours at Point: {hours} h {remainingMinutes} min");

                selectedElement.LookupParameter("Nas�onecznienie")?.Set($"{hours} h {remainingMinutes} min");

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }
}
