#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public static class GeometryExtensions
    {
        public static bool IsInsidePlane(this Line line, Plane plane)
        {
            XYZ p1 = line.GetEndPoint(0);
            XYZ p2 = line.GetEndPoint(1);

            double d1 = Math.Abs(plane.Normal.DotProduct(p1 - plane.Origin));
            double d2 = Math.Abs(plane.Normal.DotProduct(p2 - plane.Origin));

            return d1 < 0.001 && d2 < 0.001; // Jeœli ró¿nica wysokoœci jest bardzo ma³a, linia le¿y w p³aszczyŸnie
        }
    }

}



