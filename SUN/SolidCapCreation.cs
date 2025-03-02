using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using SUN;

public static class SolidCapCreation
{
    public static void CreateSolidCap(UIApplication uiapp, XYZ p1, XYZ p2, XYZ p3)
    {
        //Document doc = uiapp.ActiveUIDocument.Document;
        //// Create a TessellatedShapeBuilder
        //TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
        //tsb.OpenConnectedFaceSet(true);
        //// Create a single face for the cap using the four points
        //List<XYZ> faceVertices = new List<XYZ> { p1, p2, p3 };
        //TessellatedFace capFace = new TessellatedFace(faceVertices, ElementId.InvalidElementId);
        ////tworzenie materialu na trojkacie
        //string materialName = "Nasłonecznienie";

        //Material existingMaterial = materialExtensions.GetMaterialByName(doc, materialName);
        //ElementId newMaterialId = materialExtensions.GetOrCreateMaterial(doc, materialName);
        //if (existingMaterial != null)
        //{
        //    capFace.MaterialId = newMaterialId;
        //}
        //else
        //{
        //    capFace.MaterialId = newMaterialId;
        //}
        //// Add the face to the builder
        //tsb.AddFace(capFace);
        //tsb.CloseConnectedFaceSet();
        //// Define the builder options
        //tsb.Build();
        //// Create a DirectShape to represent the cap
        //DirectShape capShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        //capShape.SetShape(tsb.GetBuildResult().GetGeometricalObjects());


        Document doc = uiapp.ActiveUIDocument.Document;

        // 🔹 Tworzymy obiekt TessellatedShapeBuilder
        TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
        tsb.OpenConnectedFaceSet(true);

        // 🔹 Lista wierzchołków trójkąta
        List<XYZ> faceVertices = new List<XYZ> { p1, p2, p3 };
        TessellatedFace capFace = new TessellatedFace(faceVertices, ElementId.InvalidElementId);

        // 🔹 Pobieramy lub tworzymy materiał
        string materialName = "Nasłonecznienie";
        Material existingMaterial = materialExtensions.GetMaterialByName(doc, materialName);
        ElementId materialId = existingMaterial != null
            ? existingMaterial.Id
            : materialExtensions.GetOrCreateMaterial(doc, materialName);

        // 🔹 Przypisujemy materiał do trójkąta (jeśli jest dostępny)
        if (materialId != ElementId.InvalidElementId)
        {
            capFace.MaterialId = materialId;
        }
        else
        {
            TaskDialog.Show("Błąd", "Nie udało się przypisać materiału do trójkąta.");
        }

        // 🔹 Dodajemy trójkąt do TessellatedShapeBuilder
        tsb.AddFace(capFace);
        tsb.CloseConnectedFaceSet();
        tsb.Build();



        DirectShape capShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        capShape.SetShape(tsb.GetBuildResult().GetGeometricalObjects());


    }
}


