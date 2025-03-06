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
            XYZ startPoint,
            XYZ targetNormal) // <-- Wektor normalny MAMSunTarget
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "Document is null.");
            if (activeView == null)
                throw new ArgumentNullException(nameof(activeView), "Active view is null.");
            if (sunVectors == null || sunVectors.Count == 0)
                throw new ArgumentNullException(nameof(sunVectors), "Sun vectors list is empty.");
            if (startPoint == null)
                throw new ArgumentNullException(nameof(startPoint), "Start point is null.");
            if (targetNormal == null)
                throw new ArgumentNullException(nameof(targetNormal), "Target normal is null.");

            List<BuiltInCategory> categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Mass
            };

            ElementMulticategoryFilter multiFilter = new ElementMulticategoryFilter(categories);
            ReferenceIntersector refIntersector = new ReferenceIntersector(multiFilter, FindReferenceTarget.Element, activeView);

            Dictionary<ElementId, List<(XYZ intersection, int sunVectorIndex)>> elementIntersections =
                new Dictionary<ElementId, List<(XYZ, int)>>();

            XYZ firstSunVectorPoint = null;
            XYZ lastSunVectorPoint = null;

            for (int i = 0; i < sunVectors.Count; i++)
            {
                XYZ sunVector = sunVectors[i].Normalize();
                IList<ReferenceWithContext> references = refIntersector.Find(startPoint, sunVector);

                if (references == null || references.Count == 0)
                {
                    XYZ farPoint = startPoint + sunVector * 100;
                    if (i == 0) firstSunVectorPoint = farPoint;
                    if (i == sunVectors.Count - 1) lastSunVectorPoint = farPoint;
                    continue;
                }

                List<ReferenceWithContext> sortedReferences = references.OrderBy(r => r.Proximity).ToList();
                bool obstacleHit = false;

                foreach (ReferenceWithContext withContext in sortedReferences)
                {
                    Reference reference = withContext.GetReference();
                    Element element = doc.GetElement(reference);
                    if (element == null)
                        continue;

                    XYZ intersection = reference.GlobalPoint;

                    // 🔥 Sprawdzamy, czy punkt przecięcia JEST PRZED `MAMSunTarget`
                    if (!IsPointInFrontOfTarget(startPoint, intersection, targetNormal))
                        continue;

                    // Sprawdzamy, czy trafienie jest na oknie
                    if (IsPointInsideWindow(doc, intersection, out ElementId windowId))
                    {
                        if (!elementIntersections.ContainsKey(windowId))
                            elementIntersections[windowId] = new List<(XYZ, int)>();

                        elementIntersections[windowId].Add((intersection, i));
                        continue;
                    }

                    if (!IsValidIntersection(element, intersection))
                        continue;

                    if (!elementIntersections.ContainsKey(element.Id))
                        elementIntersections[element.Id] = new List<(XYZ, int)>();

                    elementIntersections[element.Id].Add((intersection, i));

                    if (i == 0) firstSunVectorPoint = intersection;
                    if (i == sunVectors.Count - 1) lastSunVectorPoint = intersection;

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

            List<GetIntersections> result = new List<GetIntersections>();

            foreach (var kvp in elementIntersections)
            {
                ElementId elemId = kvp.Key;
                List<(XYZ intersection, int sunVectorIndex)> points = kvp.Value;

                if (points.Count == 0)
                    continue;

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

            return result;
        }

        // ✅ **Poprawiona funkcja – sprawdza, czy punkt przecięcia jest przed `MAMSunTarget`**
        private bool IsPointInFrontOfTarget(XYZ startPoint, XYZ intersection, XYZ targetNormal)
        {
            XYZ vectorToIntersection = (intersection - startPoint);  // Wektor od MAMSunTarget do punktu przecięcia
            double distanceAlongNormal = vectorToIntersection.DotProduct(targetNormal);

            return distanceAlongNormal > 0; // Punkt jest PRZED `MAMSunTarget` jeśli jest w przeciwnym kierunku do normalnej
        }





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
                    if (insert != null && insert.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows)
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
    }
}
