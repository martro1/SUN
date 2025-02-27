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
        public static bool IsPointExposedToSun(Document doc, XYZ point, XYZ sunVector)
        {
            View3D activeView = doc.ActiveView as View3D;

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

            IList<ReferenceWithContext> results = refIntersector.Find(point, sunVector);


            return results.Count == 0;

        }


    }



}