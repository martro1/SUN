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
            TaskDialog.Show("Error", "FamilyInstance is null.");
            return null;
        }

        Document doc = familyInstance.Document;

        // check if familyinstace is facebased
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
                                    return face.ComputeNormal(new UV(0.5, 0.5)); // Normal vector
                                }
                            }
                        }
                    }
                }
            }
        }

        // for families not familyinstance take axis Z
        return familyInstance.GetTransform().BasisZ;
    }

}