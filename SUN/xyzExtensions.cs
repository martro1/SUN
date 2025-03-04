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
    public static class xyzExtensions
    {
        public static XYZ MoveAlongVector(
            this XYZ pointToMove, XYZ vector, double distance) =>
            pointToMove.Add(vector.Normalize() * distance);
        public static void Visualize(
            this XYZ point, Document document)
        {
            document.CreateDirectShape(Point.Create(point));
        }
        public static DirectShape CreateDirectShape
        (this Document document, IEnumerable<GeometryObject>
                geometryObjects,
            BuiltInCategory builtInCategory =
                BuiltInCategory.OST_GenericModel)
        {
            var directShape = DirectShape.CreateElement(document, new
                ElementId(builtInCategory));
            directShape.SetShape(geometryObjects.ToList());
            return directShape;
        }
        public static DirectShape CreateDirectShape(
            this Document document,
            GeometryObject geometryObject,
            BuiltInCategory builtInCategory =
                BuiltInCategory.OST_GenericModel)
        {
            var directShape = DirectShape.CreateElement(document, new
                ElementId(builtInCategory));
            directShape.SetShape(new List<GeometryObject>()
                {geometryObject});
            return directShape;
        }


    }

    public static class PointExtensions
    {
        public static XYZ SelectPoint(UIDocument uiDoc)
        {
            try
            {
                Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Face, "Select a point for sun analysis.");
                XYZ pickedPoint = pickedRef.GlobalPoint;
                return pickedPoint;
            }
            catch
            {
                return null;
            }
        }
    }

}