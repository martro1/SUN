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
    public class SunHourAnalysisKopia : IExternalCommand
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
                "Wybierz instancjê rodziny '" + targetFamilyName + "'");
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

                List<Element> e = wynik.SelectMany(w => new List<Element>() { w.Wall }).ToList();
                List<ElementId> elementIds = e.Select(e => e.Id).ToList();
                uidoc.Selection.SetElementIds(elementIds);

                List<XYZ> allPoints = wynik.SelectMany(w => new List<XYZ> { w.FirstPoint, w.LastPoint }).ToList();
                TaskDialog.Show("ilosc elementow", $"Ilosc elementow przecinajacych {wynik.Count}");
                foreach (XYZ point in allPoints)
                {
                    point.Visualize(doc);
                    XYZ planeNormal = analysisPoint.CrossProduct(point);
                    SketchPlane sketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(planeNormal, analysisPoint));
                    Curve line = Line.CreateBound(analysisPoint, point) as Curve;
                    doc.Create.NewModelCurve(line, sketchPlane);

                }

                double dist = analysisPoint.DistanceTo(wynik.First().LastPoint);
                XYZ first = analysisPoint.MoveAlongVector(oddalonelista.First(), dist);
                double dist2 = analysisPoint.DistanceTo(wynik.Last().FirstPoint);
                XYZ last = analysisPoint.MoveAlongVector(sunVectors.Last(), dist2);



                //4 przypadek 7 17 sa zasloniete
                if (!(sunExtensions.IsPointExposedToSun(doc, analysisPoint, first)) &&
                         !(sunExtensions.IsPointExposedToSun(doc, analysisPoint, last)))

                {
                    TaskDialog.Show("info", "4 przypadek 7 17 sa zasloniete");
                    if (wynik.Count >= 2)
                    {
                        for (int i = 0; i < wynik.Count - 1; i++) // Loop through pairs of adjacent intersections
                        {
                            XYZ firstWallLastPoint = wynik[i].LastPoint;
                            XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            if (i == 0)
                            {
                                firstWallLastPoint = wynik[i].FirstPoint;
                                secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            }

                            if (i == wynik.Count - 1)
                            {
                                firstWallLastPoint = wynik[i].FirstPoint;
                                secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            }

                            SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        }
                    }
                    else
                    {
                        TaskDialog.Show("wynik", "Wynik ilosci ceicia jest mniejszy od 2.");
                    }
                }


                //1 przypadek gdy 7 i 17 sa wolne
                else if ((sunExtensions.IsPointExposedToSun(doc, analysisPoint, first)) &&
                    (sunExtensions.IsPointExposedToSun(doc, analysisPoint, last)))
                {
                    TaskDialog.Show("info", "7 i 17 sa wolne");
                    if (wynik.Count >= 2)
                    {
                        XYZ godzina7 = oddalonelista.First();
                        XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);

                        for (int i = 0; i < wynik.Count - 1; i++) // Loop through pairs of adjacent intersections
                        {
                            XYZ firstWallLastPoint = wynik[i].LastPoint;
                            XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;

                            SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        }

                        XYZ godzina17 = oddalonelista.Last();
                        XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                    }
                    else
                    {
                        TaskDialog.Show("else", "Wynik ilosci ceicia jest mniejszy od 2.");
                    }
                }

                //2 przypadek 7 jest zaslonieta 17 jest wolna
                else if (!(sunExtensions.IsPointExposedToSun(doc, analysisPoint, first)) &&
                    (sunExtensions.IsPointExposedToSun(doc, analysisPoint, last)))
                {
                    TaskDialog.Show("info", "2 przypadek 7 jest zaslonieta 17 jest wolna");
                    if (wynik.Count >= 2)
                    {
                        for (int i = 0; i < wynik.Count - 1; i++) // Loop through pairs of adjacent intersections
                        {
                            XYZ firstWallLastPoint = wynik[i].LastPoint;
                            XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            if (i == 0)
                            {
                                firstWallLastPoint = wynik[i].FirstPoint;
                                secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            }
                            SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        }
                        XYZ godzina17 = oddalonelista.Last();
                        XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                    }
                    else
                    {
                        TaskDialog.Show("wynik", "Wynik ilosci ceicia jest mniejszy od 2.");
                    }
                }

                //3 przypadek 7 jest wolna 17 jest zaslonieta
                else if ((sunExtensions.IsPointExposedToSun(doc, analysisPoint, first)) &&
                    !(sunExtensions.IsPointExposedToSun(doc, analysisPoint, last)))
                {
                    TaskDialog.Show("info", "3 przypadek 7 jest wolna 17 jest zaslonieta");
                    if (wynik.Count >= 2)
                    {
                        for (int i = 0; i < wynik.Count - 1; i++) // Loop through pairs of adjacent intersections
                        {
                            XYZ firstWallLastPoint = wynik[i].LastPoint;
                            XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            if (i == 0)
                            {
                                firstWallLastPoint = wynik[i].LastPoint;
                                secondWallFirstPoint = wynik[i + 1].FirstPoint;
                            }
                            SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        }
                        XYZ godzina7 = oddalonelista.First();
                        XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);
                    }
                    else
                    {
                        TaskDialog.Show("wynik", "Wynik ilosci ceicia jest mniejszy od 2.");
                    }
                }
                else
                {
                    TaskDialog.Show("info", "zaden z przypadkow");
                }

                //DOTAD ZROBIONE


                int hours = minutes / 60;
                int remainingMinutes = minutes % 60;
                TaskDialog.Show("Sun Analysis Complete", $"Total Sun Hours at Point: {hours} hours and {remainingMinutes} minutes");

                Parameter parameter = selectedElement.LookupParameter("Nas³onecznienie");
                parameter.Set(hours + " h " + remainingMinutes + " min");

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }
}



