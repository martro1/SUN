using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUN
{
    public class WallIntersectionsKopia
    {
        public Element Wall { get; set; }
        public XYZ FirstPoint { get; set; }
        public XYZ LastPoint { get; set; }

        public List<WallIntersectionsKopia> GetWallIntersections(Document doc, View3D activeView, List<XYZ> sunVectors, XYZ startPoint)
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Mass
        };

            ElementMulticategoryFilter multiFilter = new ElementMulticategoryFilter(categories);
            ReferenceIntersector refIntersector = new ReferenceIntersector(multiFilter, FindReferenceTarget.Element, activeView);

            Dictionary<ElementId, List<XYZ>> wallIntersections = new Dictionary<ElementId, List<XYZ>>();

            foreach (XYZ sunVector in sunVectors)
            {
                IList<ReferenceWithContext> references = refIntersector.Find(startPoint, sunVector);

                foreach (ReferenceWithContext withContext in references)
                {
                    Reference reference = withContext.GetReference();
                    Element wall = doc.GetElement(reference);
                    XYZ intersection = reference.GlobalPoint;

                    if (wall != null)
                    {
                        if (!wallIntersections.ContainsKey(wall.Id))
                        {
                            wallIntersections[wall.Id] = new List<XYZ>();
                        }
                        wallIntersections[wall.Id].Add(intersection);
                    }
                }
            }

            // Sort points along sun vector direction and extract first/last per wall
            List<WallIntersectionsKopia> result = new List<WallIntersectionsKopia>();

            foreach (var kvp in wallIntersections)
            {
                List<XYZ> points = kvp.Value;

                // Sort points along sun vector projection
                points = points.OrderBy(p => (p - startPoint).DotProduct(sunVectors.First())).ToList();

                if (points.Count > 1)
                {
                    result.Add(new WallIntersectionsKopia
                    {
                        Wall = doc.GetElement(kvp.Key),
                        FirstPoint = points.Last(),
                        LastPoint = points.First()
                    });
                }
            }

            return result;
        }

    }
}
