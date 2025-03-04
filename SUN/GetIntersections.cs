using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SUN
{
    public class GetIntersections
    {
        public Element Wall { get; set; }
        public XYZ FirstPoint { get; set; }
        public XYZ LastPoint { get; set; }
        public string FirstSunRayHour { get; set; }
        public string LastSunRayHour { get; set; }

        public List<GetIntersections> GetIntersection(
    Document doc,
    View3D activeView,
    List<XYZ> sunVectors,
    XYZ startPoint)
        {
            // SPRAWDZANIE NULL
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "Document is null. Check ExternalCommandData.");
            if (activeView == null)
                throw new ArgumentNullException(nameof(activeView), "Active view is null. Ensure View3D is used.");
            if (sunVectors == null || sunVectors.Count == 0)
                throw new ArgumentNullException(nameof(sunVectors), "Sun vectors list is null or empty.");
            if (startPoint == null)
                throw new ArgumentNullException(nameof(startPoint), "Start point is null.");

            // Kategorie traktowane jako nieprzezroczyste
            List<BuiltInCategory> categories = new List<BuiltInCategory>
    {
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Mass
    };

            ElementMulticategoryFilter multiFilter =
                new ElementMulticategoryFilter(categories);

            // SPRAWDZENIE, CZY activeView JEST 3D VIEW
            if (!(activeView is View3D))
                throw new InvalidOperationException("Active view must be a 3D view.");

            // RefIntersector dla widoku 3D
            ReferenceIntersector refIntersector =
                new ReferenceIntersector(multiFilter, FindReferenceTarget.Element, activeView);

            // SPRAWDZAMY, CZY GenLevel JEST NULL
            SketchPlane sketchPlane = null;
            try
            {
                if (activeView.GenLevel != null)
                {
                    // Jeśli GenLevel istnieje, używamy go
                    sketchPlane = SketchPlane.Create(doc, activeView.GenLevel.Id);
                }
                else
                {
                    // Jeśli GenLevel nie istnieje, tworzymy płaszczyznę na podstawie położenia widoku
                    Plane plane = Plane.CreateByNormalAndOrigin(activeView.ViewDirection, activeView.Origin);
                    sketchPlane = SketchPlane.Create(doc, plane);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create SketchPlane. Ensure activeView is valid.", ex);
            }

            // Inicjalizacja słownika na przecięcia
            Dictionary<ElementId, List<(XYZ intersection, int sunVectorIndex)>> elementIntersections =
                new Dictionary<ElementId, List<(XYZ, int)>>();

            // Kontynuacja kodu...


            // Pamiętamy pierwszy i ostatni punkt na promieniach
            XYZ firstSunVectorPoint = null;
            XYZ lastSunVectorPoint = null;

            // -------------------------------------------------------------------------
            // 1) GŁÓWNA PĘTLA: iterujemy po wszystkich wektorach słońca
            // -------------------------------------------------------------------------
            for (int i = 0; i < sunVectors.Count; i++)
            {
                XYZ sunVector = sunVectors[i].Normalize();
                IList<ReferenceWithContext> references = refIntersector.Find(startPoint, sunVector);

                if (references == null || references.Count == 0)
                {
                    // Jeśli promień nie trafił w nic – ustawiamy jego koniec jako daleki punkt
                    XYZ farPoint = startPoint + sunVector * 100; // Punkt daleko poza sceną
                    if (i == 0) firstSunVectorPoint = farPoint;
                    if (i == sunVectors.Count - 1) lastSunVectorPoint = farPoint;
                    continue;
                }

                // Sortujemy trafienia wg odległości
                List<ReferenceWithContext> sortedReferences = references
                    .OrderBy(r => r.Proximity)
                    .ToList();

                bool obstacleHit = false;

                // 1A) Iteracja po trafieniach (od najbliższego do najdalszego)
                foreach (ReferenceWithContext withContext in sortedReferences)
                {
                    Reference reference = withContext.GetReference();
                    Element element = doc.GetElement(reference);
                    if (element == null)
                        continue;

                    XYZ intersection = reference.GlobalPoint;

                    // Sprawdzamy, czy trafienie jest na oknie
                    if (IsPointInsideWindow(doc, intersection, out ElementId windowId))
                    {
                        if (!elementIntersections.ContainsKey(windowId))
                            elementIntersections[windowId] = new List<(XYZ, int)>();

                        elementIntersections[windowId].Add((intersection, i));
                        continue;
                    }

                    // Sprawdzamy, czy to poprawne trafienie
                    if (!IsValidIntersection(element, intersection))
                        continue;

                    if (!elementIntersections.ContainsKey(element.Id))
                        elementIntersections[element.Id] = new List<(XYZ, int)>();

                    elementIntersections[element.Id].Add((intersection, i));

                    // Zapamiętujemy punkt dla pierwszego i ostatniego promienia
                    if (i == 0) firstSunVectorPoint = intersection;
                    if (i == sunVectors.Count - 1) lastSunVectorPoint = intersection;

                    // Jeśli to pierwsza przeszkoda, zatrzymujemy promień
                    if (!obstacleHit)
                    {
                        obstacleHit = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // -------------------------------------------------------------------------
            // 2) Budujemy wynik: tylko pierwszy i ostatni punkt na danej przeszkodzie
            // -------------------------------------------------------------------------
            List<GetIntersections> result = new List<GetIntersections>();

            foreach (var kvp in elementIntersections)
            {
                ElementId elemId = kvp.Key;
                List<(XYZ intersection, int sunVectorIndex)> points = kvp.Value;

                if (points.Count == 0)
                    continue;

                // Sortujemy punkty wzdłuż promienia słonecznego
                var sortedPoints = points.OrderBy(p => p.intersection.X).ToList();

                XYZ firstPoint = sortedPoints.First().intersection;
                XYZ lastPoint = sortedPoints.Last().intersection;

                int firstSunVectorIndex = sortedPoints.First().sunVectorIndex;
                int lastSunVectorIndex = sortedPoints.Last().sunVectorIndex;

                string firstHour = firstSunVectorIndex == 0 ? "07:00" : $"{7 + firstSunVectorIndex}:00";
                string lastHour = lastSunVectorIndex == sunVectors.Count - 1 ? "17:00" : $"{7 + lastSunVectorIndex}:00";

                result.Add(new GetIntersections
                {
                    Wall = doc.GetElement(elemId),
                    FirstPoint = firstPoint,
                    LastPoint = lastPoint,
                    FirstSunRayHour = firstHour,
                    LastSunRayHour = lastHour
                });
            }

            // -------------------------------------------------------------------------
            // 3) Tworzenie ModelCurve dla pierwszego i ostatniego promienia słońca
            // -------------------------------------------------------------------------
            if (firstSunVectorPoint != null && !startPoint.IsAlmostEqualTo(firstSunVectorPoint))
            {
                XYZ vec = (firstSunVectorPoint - startPoint).Normalize();

                if (!vec.IsZeroLength())
                {
                    XYZ normal = null;

                    // Sprawdzamy, czy analizowany punkt leży na ścianie
                    Element firstElement = GetElementAtPoint(doc, firstSunVectorPoint);
                    if (firstElement is Wall wall)
                    {
                        LocationCurve wallCurve = wall.Location as LocationCurve;
                        if (wallCurve != null)
                        {
                            XYZ wallDirection = (wallCurve.Curve as Line).Direction.Normalize();
                            normal = wallDirection.CrossProduct(vec).Normalize();
                        }
                    }
                    else
                    {
                        normal = vec.CrossProduct(XYZ.BasisZ).Normalize();
                    }

                    // Awaryjne zabezpieczenie
                    if (normal.IsZeroLength()) normal = XYZ.BasisX;

                    // Tworzymy płaszczyznę tylko, jeśli normalna nie jest zerowa
                    if (!normal.IsZeroLength())
                    {
                        Plane firstPlane = Plane.CreateByNormalAndOrigin(normal, startPoint);
                        SketchPlane firstSketchPlane = SketchPlane.Create(doc, firstPlane);

                        Line firstSunLine = Line.CreateBound(startPoint, firstSunVectorPoint);

                        // Sprawdzamy, czy linia leży w płaszczyźnie
                        if (IsLineInPlane(firstSunLine, firstPlane))
                        {
                            doc.Create.NewModelCurve(firstSunLine, firstSketchPlane);
                        }
                    }
                }
            }

            if (lastSunVectorPoint != null && !startPoint.IsAlmostEqualTo(lastSunVectorPoint))
            {
                XYZ vec = (lastSunVectorPoint - startPoint).Normalize();

                if (!vec.IsZeroLength())
                {
                    XYZ normal = null;

                    Element lastElement = GetElementAtPoint(doc, lastSunVectorPoint);
                    if (lastElement is Wall wall)
                    {
                        LocationCurve wallCurve = wall.Location as LocationCurve;
                        if (wallCurve != null)
                        {
                            XYZ wallDirection = (wallCurve.Curve as Line).Direction.Normalize();
                            normal = wallDirection.CrossProduct(vec).Normalize();
                        }
                    }
                    else
                    {
                        normal = vec.CrossProduct(XYZ.BasisZ).Normalize();
                    }

                    if (normal.IsZeroLength()) normal = XYZ.BasisX;

                    if (!normal.IsZeroLength())
                    {
                        Plane lastPlane = Plane.CreateByNormalAndOrigin(normal, startPoint);
                        SketchPlane lastSketchPlane = SketchPlane.Create(doc, lastPlane);

                        Line lastSunLine = Line.CreateBound(startPoint, lastSunVectorPoint);

                        if (IsLineInPlane(lastSunLine, lastPlane))
                        {
                            doc.Create.NewModelCurve(lastSunLine, lastSketchPlane);
                        }
                    }
                }
            }











            return result;
        }

        // -------------------------------------------------------------------------
        // Pomocnicza metoda do sprawdzania geometrii
        // -------------------------------------------------------------------------
        private bool IsValidIntersection(Element element, XYZ intersection)
        {
            Options opt = new Options();
            GeometryElement geoElement = element.get_Geometry(opt);
            if (geoElement == null)
                return false;

            foreach (GeometryObject geoObj in geoElement)
            {
                if (geoObj is Solid solid && solid.Volume > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face.Project(intersection) != null)
                            return true;
                    }
                }
            }
            return false;
        }

        // -------------------------------------------------------------------------
        // Metoda do sprawdzania, czy punkt jest wewnątrz BoundingBox
        // -------------------------------------------------------------------------
        private bool IsPointInsideBoundingBox(BoundingBoxXYZ bbox, XYZ point)
        {
            if (bbox == null)
                return false;

            XYZ min = bbox.Min;
            XYZ max = bbox.Max;

            return (point.X >= min.X && point.X <= max.X &&
                    point.Y >= min.Y && point.Y <= max.Y &&
                    point.Z >= min.Z && point.Z <= max.Z);
        }
        private bool IsPointInsideWindow(Document doc, XYZ intersection, out ElementId windowId)
        {
            windowId = ElementId.InvalidElementId;

            FilteredElementCollector wallCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            foreach (Wall wall in wallCollector)
            {
                List<ElementId> inserts = wall.FindInserts(true, false, false, false).ToList();

                foreach (ElementId insertId in inserts)
                {
                    FamilyInstance insert = doc.GetElement(insertId) as FamilyInstance;
                    if (insert != null &&
                        insert.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows)
                    {
                        BoundingBoxXYZ bbox = insert.get_BoundingBox(null);
                        if (bbox != null && IsPointInsideBoundingBox(bbox, intersection))
                        {
                            windowId = insert.Id;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private Element GetElementAtPoint(Document doc, XYZ point)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                Wall wall = elem as Wall;
                if (wall == null) continue;

                BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                if (bbox != null && IsPointInsideBoundingBox(bbox, point))
                {
                    return wall; // Znaleziono ścianę, zwracamy element
                }
            }

            return null; // Jeśli nie znaleziono ściany, zwracamy null
        }

        private bool IsLineInPlane(Line line, Plane plane)
        {
            XYZ p1 = line.GetEndPoint(0);
            XYZ p2 = line.GetEndPoint(1);

            double d1 = Math.Abs(plane.Normal.DotProduct(p1 - plane.Origin));
            double d2 = Math.Abs(plane.Normal.DotProduct(p2 - plane.Origin));

            return d1 < 0.001 && d2 < 0.001; // Jeśli różnica jest mała, linia leży w płaszczyźnie
        }



    }
}
