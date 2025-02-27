using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUN
{
    public class GetIntersections
    {
        public Element Wall { get; set; }
        public XYZ FirstPoint { get; set; }
        public XYZ LastPoint { get; set; }

        public List<GetIntersections> GetIntersection(Document doc, View3D activeView, List<XYZ> sunVectors,
            XYZ startPoint)
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Mass
            };

            ElementMulticategoryFilter multiFilter = new ElementMulticategoryFilter(categories);
            ReferenceIntersector refIntersector =
                new ReferenceIntersector(multiFilter, FindReferenceTarget.Element, activeView);

            Dictionary<ElementId, List<XYZ>> wallIntersections = new Dictionary<ElementId, List<XYZ>>();
            Dictionary<ElementId, List<XYZ>> windowIntersections = new Dictionary<ElementId, List<XYZ>>();

            foreach (XYZ sunVector in sunVectors)
            {
                IList<ReferenceWithContext> references = refIntersector.Find(startPoint, sunVector);

                foreach (ReferenceWithContext withContext in references)
                {
                    Reference reference = withContext.GetReference();
                    Element element = doc.GetElement(reference);
                    XYZ intersection = reference.GlobalPoint;

                    if (element != null && element is Wall wall)
                    {
                        // Jeśli punkt przecięcia znajduje się w otworze okna, zapisujemy go i kontynuujemy analizę!
                        if (IsPointInsideWindow(doc, intersection, out ElementId windowId))
                        {
                            if (!windowIntersections.ContainsKey(windowId))
                            {
                                windowIntersections[windowId] = new List<XYZ>();
                            }

                            windowIntersections[windowId].Add(intersection);
                            continue; // NIE przerywamy analizy, ale przechodzimy do kolejnego przecięcia!
                        }

                        // Pobierz geometrię ściany
                        Options opt = new Options();
                        GeometryElement geoElement = wall.get_Geometry(opt);
                        bool isValidIntersection = false;

                        foreach (GeometryObject geoObj in geoElement)
                        {
                            if (geoObj is Solid solid && solid.Volume > 0)
                            {
                                foreach (Face face in solid.Faces)
                                {
                                    if (face.Project(intersection) != null)
                                    {
                                        isValidIntersection = true;
                                        break;
                                    }
                                }
                            }

                            if (isValidIntersection) break;
                        }

                        if (isValidIntersection)
                        {
                            if (!wallIntersections.ContainsKey(wall.Id))
                            {
                                wallIntersections[wall.Id] = new List<XYZ>();
                            }

                            wallIntersections[wall.Id].Add(intersection);
                        }
                    }
                }
            }

            // Tworzymy listę wynikową
            List<GetIntersections> result = new List<GetIntersections>();

            // Dodajemy punkty w otworach okiennych
            foreach (var kvp in windowIntersections)
            {
                // Pobieramy pierwszy wektor słońca i normalizujemy go
                XYZ sunVector = sunVectors.First().Normalize();

                // Pobieramy wszystkie punkty przecięcia
                List<XYZ> points = kvp.Value.OrderBy(p => (p - startPoint).DotProduct(sunVector)).ToList();

                if (points.Count > 1)
                {
                    // **Znajdujemy najniższą wartość Z wśród punktów otworu**
                    double minZ = points.Max(pt => pt.Z); // Find lowest Z, not highest


                    // **Filtrujemy punkty, wybierając tylko te na dolnej krawędzi otworu**
                    List<XYZ> bottomEdgePoints = points.Where(p => Math.Abs(p.Z - minZ) < 0.05).ToList();


                    // **Znajdujemy najbardziej zewnętrzne punkty otworu (blisko elewacji)**
                    double maxDepth = bottomEdgePoints.Max(p => p.Y); // Zakładamy, że większy Y oznacza bardziej na zewnątrz

                    List<XYZ> outerEdgePoints = bottomEdgePoints.Where(p => Math.Abs(p.Y - maxDepth) < 0.01).ToList();

                    // **Wybieramy najbardziej skrajne punkty na zewnętrznych dolnych krawędziach otworu**
                    XYZ firstPoint = outerEdgePoints.OrderBy(p => p.X).FirstOrDefault();
                    XYZ lastPoint = outerEdgePoints.OrderBy(p => p.X).LastOrDefault();

                    // **Jeśli punkty są null, wybieramy awaryjnie dwa skrajne punkty dolnej krawędzi**
                    if (firstPoint == null || lastPoint == null || firstPoint.IsAlmostEqualTo(lastPoint))
                    {
                        firstPoint = bottomEdgePoints.OrderBy(p => p.X).First();
                        lastPoint = bottomEdgePoints.OrderBy(p => p.X).Last();
                    }

                    // **Dodajemy poprawnie wybrane punkty do wyniku**
                    result.Add(new GetIntersections
                    {
                        Wall = doc.GetElement(kvp.Key),
                        FirstPoint = firstPoint, // Lewy dolny róg otworu (zewnętrzna krawędź)
                        LastPoint = lastPoint    // Prawy dolny róg otworu (zewnętrzna krawędź)
                    });
                }
            }






























            // Dodajemy punkty na ścianach
            foreach (var kvp in wallIntersections)
            {
                List<XYZ> points = kvp.Value.OrderBy(p => (p - startPoint).DotProduct(sunVectors.First())).ToList();
                if (points.Count > 1)
                {
                    result.Add(new GetIntersections
                    {
                        Wall = doc.GetElement(kvp.Key),
                        FirstPoint = points.Last(),
                        LastPoint = points.First()
                    });
                }
            }

            return result;
        }

        private bool IsPointInsideWindow(Document doc, XYZ intersection, out ElementId windowId)
        {
            windowId = ElementId.InvalidElementId;

            // Pobierz wszystkie ściany zawierające otwory
            FilteredElementCollector wallCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            foreach (Wall wall in wallCollector)
            {
                // Znajdź wszystkie elementy osadzone w ścianie (okna, drzwi itp.)
                List<ElementId> inserts = wall.FindInserts(true, false, false, false).ToList();

                foreach (ElementId insertId in inserts)
                {
                    FamilyInstance insert = doc.GetElement(insertId) as FamilyInstance;
                    if (insert != null && insert.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows)
                    {
                        // Pobranie Bounding Box okna
                        BoundingBoxXYZ bbox = insert.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            XYZ min = bbox.Min;
                            XYZ max = bbox.Max;

                            // Sprawdzenie, czy punkt przecięcia mieści się w zakresie Bounding Box okna
                            if (intersection.X >= min.X && intersection.X <= max.X &&
                                intersection.Y >= min.Y && intersection.Y <= max.Y &&
                                intersection.Z >= min.Z && intersection.Z <= max.Z)
                            {
                                windowId = insert.Id;
                                return true; // Punkt jest w otworze okna
                            }
                        }
                    }
                }
            }

            return false; // Punkt znajduje się na ścianie
        }
    }
    }
