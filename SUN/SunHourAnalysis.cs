﻿#region Namespaces
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
    public class SunHourAnalysis : IExternalCommand
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
                "Wybierz instancję rodziny '" + targetFamilyName + "'");
            Element selectedElement = doc.GetElement(pickedRef);
            LocationPoint locP = selectedElement.Location as LocationPoint;
            XYZ locationPPoint = locP.Point;

            using (Transaction trans = new Transaction(doc, "Analiza nasłonecznienia"))
            {
                trans.Start();

                FamilyInstance familyInstance = selectedElement as FamilyInstance;
                XYZ normal = FamilyInstanceNormal.GetFamilyNormal(familyInstance).Normalize();
                double odsunieciePunktuwFeetach = selectedElement.LookupParameter("od").AsDouble();
                XYZ analysisPoint = locationPPoint.MoveAlongVector(normal, -odsunieciePunktuwFeetach);

                foreach (XYZ sunVector in sunVectors)
                {
                    XYZ oddalone = analysisPoint + sunVector * 500;
                    oddalonelista.Add(oddalone);
                    if (sunExtensions.IsPointExposedToSun(doc, analysisPoint, oddalone))
                    {
                        minutes++;
                    }
                }

                TaskDialog.Show("Normal", $"Normal MAMSunTarget: {normal.X}, {normal.Y}, {normal.Z}");

                GetIntersections WI = new GetIntersections();
                List<GetIntersections> wynik = WI.GetIntersection(doc, activeView, oddalonelista, analysisPoint,normal);

                if (wynik.Count < 2)
                {
                    TaskDialog.Show("Błąd", "Za mało przecięć do analizy. Sprawdź ustawienia.");
                    trans.RollBack();
                    return Result.Failed;
                }

                // Szukamy rzeczywistego przecięcia pierwszego i ostatniego wektora słonecznego
                XYZ firstIntersection = wynik.FirstOrDefault()?.LastPoint ?? oddalonelista.First();
                XYZ lastIntersection = wynik.LastOrDefault()?.FirstPoint ?? oddalonelista.Last();

                // Sprawdzamy, czy pierwsza i ostatnia godzina są zasłonięte
                bool firstBlocked = !sunExtensions.IsPointExposedToSun(doc, analysisPoint, firstIntersection);
                bool lastBlocked = !sunExtensions.IsPointExposedToSun(doc, analysisPoint, lastIntersection);


                // ✅ Wizualizacja punktów przecięcia
                foreach (var intersection in wynik)
                {
                    intersection.FirstPoint.Visualize(doc);
                    intersection.LastPoint.Visualize(doc);
                }

                // ✅ Tworzenie trójkątów dla 4 przypadków
                int createdTriangles = 0;

                // 🔹 1. Oba wektory zasłonięte (7:00 i 17:00)
                if (firstBlocked && lastBlocked)
                {
                    TaskDialog.Show("Info", "Oba wektory (7:00 i 17:00) są zasłonięte.");
                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;

                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        createdTriangles++;
                    }
                }
                // 🔹 2. `7:00` odsłonięta, `17:00` zasłonięta
                else if (!firstBlocked && lastBlocked)
                {
                    TaskDialog.Show("Info", "Wektor 7:00 jest odsłonięty, 17:00 zasłonięty.");

                    XYZ godzina7 = oddalonelista.First();
                    XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);

                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        createdTriangles++;
                    }
                }
                // 🔹 3. `7:00` zasłonięta, `17:00` odsłonięta
                else if (firstBlocked && !lastBlocked)
                {
                    TaskDialog.Show("Info", "Wektor 7:00 jest zasłonięty, 17:00 odsłonięty.");
                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        createdTriangles++;
                    }

                    XYZ godzina17 = oddalonelista.Last();
                    XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                }
                // 🔹 4. Oba wektory odsłonięte
                else
                {
                    TaskDialog.Show("Info", "Oba wektory (7:00 i 17:00) są odsłonięte.");

                    XYZ godzina7 = oddalonelista.First();
                    XYZ firstWallFirstPoint = wynik[0].FirstPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallFirstPoint, godzina7);

                    for (int i = 0; i < wynik.Count - 1; i++)
                    {
                        XYZ firstWallLastPoint = wynik[i].LastPoint;
                        XYZ secondWallFirstPoint = wynik[i + 1].FirstPoint;
                        SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstWallLastPoint, secondWallFirstPoint);
                        createdTriangles++;
                    }

                    XYZ godzina17 = oddalonelista.Last();
                    XYZ lastWallLastPoint = wynik[wynik.Count - 1].LastPoint;
                    SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, lastWallLastPoint, godzina17);
                }

                int hours = minutes / 60;
                int remainingMinutes = minutes % 60;
                TaskDialog.Show("Sun Analysis Complete", $"Total Sun Hours at Point: {hours} h {remainingMinutes} min");

                selectedElement.LookupParameter("Nasłonecznienie")?.Set($"{hours} h {remainingMinutes} min");

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }
}
