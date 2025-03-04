using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using SUN;

public static class SolidCapCreation
{
    //public static void CreateSolidCap(UIApplication uiapp, XYZ p1, XYZ p2, XYZ p3)
    //{
    //    Document doc = uiapp.ActiveUIDocument.Document;
    //    // Create a TessellatedShapeBuilder
    //    TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
    //    tsb.OpenConnectedFaceSet(true);
    //    // Create a single face for the cap using the four points
    //    List<XYZ> faceVertices = new List<XYZ> { p1, p2, p3 };
    //    TessellatedFace capFace = new TessellatedFace(faceVertices, ElementId.InvalidElementId);
    //    //tworzenie materialu na trojkacie
    //    string materialName = "Nasłonecznienie";
    //    Material existingMaterial = materialExtensions.GetMaterialByName(doc, materialName);
    //    ElementId newMaterialId = materialExtensions.GetOrCreateMaterial(doc, materialName);
    //    if (existingMaterial != null)
    //    {
    //        capFace.MaterialId = newMaterialId;
    //    }
    //    else
    //    {
    //        capFace.MaterialId = newMaterialId;
    //    }
    //    // Add the face to the builder
    //    tsb.AddFace(capFace);
    //    tsb.CloseConnectedFaceSet();
    //    // Define the builder options
    //    tsb.Build();
    //    // Create a DirectShape to represent the cap
    //    DirectShape capShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
    //    capShape.SetShape(tsb.GetBuildResult().GetGeometricalObjects());
    //    }




    public static void CreateSolidCap(UIApplication uiapp, XYZ p1, XYZ p2, XYZ p3)
    {
        Document doc = uiapp.ActiveUIDocument.Document;

        // Sprawdzenie czy punkty są poprawne
        if (p1.IsAlmostEqualTo(p2) || p1.IsAlmostEqualTo(p3) || p2.IsAlmostEqualTo(p3))
        {
            TaskDialog.Show("Błąd", "Punkty trójkąta są identyczne. Pominięto tworzenie.");
            return;
        }

        // Sortowanie punktów, aby mieć poprawną kolejność
        List<XYZ> sortedPoints = SortPointsByDistance(p1, p2, p3);
        p1 = sortedPoints[0];
        p2 = sortedPoints[1];
        p3 = sortedPoints[2];

        // Sprawdzanie, czy punkty leżą w jednej płaszczyźnie
        XYZ normal = (p2 - p1).CrossProduct(p3 - p1).Normalize();
        if (normal.IsZeroLength())
        {
            TaskDialog.Show("Błąd", "Punkty nie tworzą prawidłowej płaszczyzny.");
            return;
        }

        // Tworzenie TessellatedShapeBuilder
        TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
        tsb.OpenConnectedFaceSet(true);

        // Tworzenie trójkąta
        List<XYZ> faceVertices = new List<XYZ> { p1, p2, p3 };
        TessellatedFace capFace = new TessellatedFace(faceVertices, ElementId.InvalidElementId);

        // Dodawanie materiału
        string materialName = "Nasłonecznienie";
        Material existingMaterial = materialExtensions.GetMaterialByName(doc, materialName);
        ElementId newMaterialId = materialExtensions.GetOrCreateMaterial(doc, materialName);
        if (existingMaterial != null)
        {
            capFace.MaterialId = newMaterialId;
        }
        else
        {
            capFace.MaterialId = newMaterialId;
        }

        // Dodanie twarzy do obiektu
        tsb.AddFace(capFace);
        tsb.CloseConnectedFaceSet();
        tsb.Build();

        // Tworzenie DirectShape (bez transakcji)
        DirectShape capShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        capShape.SetShape(tsb.GetBuildResult().GetGeometricalObjects());

        // Regeneracja modelu, ale bez transakcji - transakcja powinna być zarządzana w kodzie głównym
        doc.Regenerate();
    }

    // Metoda do sortowania punktów w poprawnej kolejności
    private static List<XYZ> SortPointsByDistance(XYZ p1, XYZ p2, XYZ p3)
    {
        List<XYZ> points = new List<XYZ> { p1, p2, p3 };
        points.Sort((a, b) => a.DistanceTo(XYZ.Zero).CompareTo(b.DistanceTo(XYZ.Zero)));
        return points;
    }
}

