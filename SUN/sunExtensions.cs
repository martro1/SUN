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

            ElementClassFilter filter = new ElementClassFilter(typeof(Wall));
            ReferenceIntersector refIntersector =
                new ReferenceIntersector(filter, FindReferenceTarget.All, activeView);

            IList<ReferenceWithContext> results = refIntersector.Find(point, -sunVector);


            return results.Count == 1;

        }


    }



}