#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
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
            XYZ analysisPoint = PointExtensions.SelectPoint(uidoc);
            if (analysisPoint == null)
            {
                TaskDialog.Show("Cancelled", "No point selected.");
                return Result.Cancelled;
            }
            using (Transaction trans = new Transaction(doc, "dada"))
            {
                trans.Start();
                foreach (XYZ sunVector in sunVectors)
                {
                    XYZ zero = XYZ.Zero;
                    oddalone = zero.MoveAlongVector(sunVector, 1000);
                    if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, -oddalone))
                    {
                        minutes++;
                    }
                }
                WallIntersections WI = new WallIntersections();
                List<WallIntersections> wynik = WI.GetWallIntersections(doc, activeView, sunVectors, analysisPoint);



                if (wynik.Count >= 2)
                {
                    for (int i = 0; i < wynik.Count - 1; i++) // Loop through pairs of adjacent intersections
                    {
                        XYZ firstWallLastPoint = wynik[i].FirstPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].LastPoint;

                        // Create a solid cap between adjacent walls
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                    }
                }
                double dist = analysisPoint.DistanceTo(wynik.First().LastPoint);
                XYZ first = analysisPoint.MoveAlongVector(sunVectors.First(), dist);
                if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, first) == false)
                {
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, first, wynik.First().LastPoint);
                }

  
                double dist2 = analysisPoint.DistanceTo(wynik.Last().FirstPoint);
                XYZ last = analysisPoint.MoveAlongVector(sunVectors.Last(), dist2);

                wynik.Last().FirstPoint.Visualize(doc);
                last.Visualize(doc);
                if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, last) == false)
                {
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, last, wynik.Last().FirstPoint);
                }




                trans.Commit();
            }

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;
            TaskDialog.Show("Sun Analysis Complete", $"Total Sun Hours at Point: {hours} hours and {remainingMinutes} minutes");


            return Result.Succeeded;
        }
    }
}



