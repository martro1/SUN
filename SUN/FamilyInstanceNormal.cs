using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

public class FamilyInstanceNormal
{
    public static XYZ GetFamilyNormal(FamilyInstance familyInstance)
    {
        if (familyInstance == null)
        {
            TaskDialog.Show("Błąd", "FamilyInstance jest null.");
            return null;
        }

        Document doc = familyInstance.Document;

        // 1️⃣ **Sprawdź, czy rodzina jest Face-Based**
        Reference reference = familyInstance.HostFace;
        if (reference != null)
        {
            Element hostElement = doc.GetElement(reference.ElementId);
            if (hostElement != null)
            {
                Options options = new Options { ComputeReferences = true };
                GeometryElement geomElement = hostElement.get_Geometry(options);

                if (geomElement != null)
                {
                    foreach (GeometryObject geomObj in geomElement)
                    {
                        if (geomObj is Solid solid)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                if (face.Reference != null && face.Reference.ElementId == reference.ElementId)
                                {
                                    return face.ComputeNormal(new UV(0.5, 0.5)); // Wektor normalny Face-Based
                                }
                            }
                        }
                    }
                }
            }
        }

        // 2️⃣ **Dla rodzin NIE Face-Based, pobierz oś Z z transformacji**
        return familyInstance.GetTransform().BasisZ;
    }

}