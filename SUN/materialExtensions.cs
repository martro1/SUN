#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media.Media3D;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Material = Autodesk.Revit.DB.Material;

#endregion

namespace SUN
{
    [Transaction(TransactionMode.Manual)]
    public static class materialExtensions
    {
        public static Material GetMaterialByName(Document doc, string materialName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            Material material = collector.OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));

            return material; // Zwraca materia³, jeœli istnieje, w przeciwnym razie null
        }
        public static ElementId GetOrCreateMaterial(Document doc, string materialName)
        {
            // Sprawdzenie, czy materia³ ju¿ istnieje
            Material existingMaterial = GetMaterialByName(doc, materialName);
            if (existingMaterial != null)
            {
                return existingMaterial.Id; // Jeœli istnieje, zwracamy jego ID
            }

            // Tworzenie nowego materia³u
            ElementId elementmat = Material.Create(doc, materialName);
            Material newMaterial = doc.GetElement(elementmat) as Material;
            newMaterial.Color = new Color(255, 128, 0); // autocad 30
            newMaterial.Transparency = 50; // 20% przezroczystoœci


            return newMaterial.Id;
        }
    }


}



