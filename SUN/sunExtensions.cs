#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace SUN
{
    [Transaction(TransactionMode.Manual)]
    public static class sunExtensions
    {

        //public static XYZ GetSunVector(double altitude, double azimuth)
        //{
        //    double altRad = altitude * (Math.PI / 180); // Konwersja stopni na radiany
        //    double azRad = azimuth * (Math.PI / 180);

        //    double x = Math.Cos(altRad) * Math.Sin(azRad);
        //    double y = Math.Cos(altRad) * Math.Cos(azRad);
        //    double z = Math.Sin(altRad);

        //    return new XYZ(x, y, z);
        //}
        public static XYZ GetSunVector(double altitude, double azimuth)
        {
            double x = Math.Cos(altitude) * Math.Sin(azimuth);
            double y = Math.Cos(altitude) * Math.Cos(azimuth);
            double z = Math.Sin(altitude);

            return new XYZ(x, y, z);
        }

        public static bool IsPointExposedToSun(Document doc, XYZ startPoint, XYZ targetPoint)
        {
            View3D view3D = doc.ActiveView as View3D;

            // Filtrujemy przeszkody – œciany, dachy, pod³ogi, masy
            List<BuiltInCategory> categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Mass
            };

            ElementMulticategoryFilter multiFilter = new ElementMulticategoryFilter(categories);
            ReferenceIntersector intersector =
                new ReferenceIntersector(multiFilter, FindReferenceTarget.Element, view3D);

            // Wyszukujemy przeszkody na œcie¿ce promienia s³oñca
            IList<ReferenceWithContext> references =
                intersector.Find(startPoint, (targetPoint - startPoint).Normalize());
            if (references == null || references.Count == 0)
            {
                return true; // Jeœli nie ma przeszkód, punkt jest ods³oniêty
            }

            // Sortujemy trafienia wg odleg³oœci
            references = references.OrderBy(r => r.Proximity).ToList();

            foreach (ReferenceWithContext refContext in references)
            {
                Reference reference = refContext.GetReference();
                Element element = doc.GetElement(reference);
                if (element == null) continue;

                // Jeœli przeszkoda jest bli¿ej ni¿ docelowy punkt, oznacza to, ¿e s³oñce jest zas³oniête
                if (refContext.Proximity < startPoint.DistanceTo(targetPoint))
                {
                    return false; // Punkt jest zas³oniêty
                }
            }

            return true; // Jeœli nie by³o wczeœniejszej przeszkody, punkt jest ods³oniêty
        }




    }
}