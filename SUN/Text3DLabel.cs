using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

namespace SUN
{
    public class Text3DLabel
    {
        public static void CreateText3D(Document doc, XYZ position, string text)
        {


                // Pobranie domyœlnej rodziny tekstu 3D (lub u¿yj w³asnej)
                ElementId textNoteTypeId = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .FirstElementId();

                if (textNoteTypeId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show("B³¹d", "Nie znaleziono domyœlnej rodziny Text Note.");
                    return;
                }

                // Tworzenie obiektu TextNote 3D
                TextNoteOptions options = new TextNoteOptions(textNoteTypeId);
                options.Rotation = 0;

                TextNote textNote = TextNote.Create(doc, doc.ActiveView.Id, position, text, options);


            
        }

        public static void NumberPoints(Document doc, List<GetIntersections> intersections)
        {
            int index = 1;
            foreach (var intersection in intersections)
            {
                XYZ firstTextPosition = intersection.FirstPoint + new XYZ(0, 0, 0.3);
                XYZ lastTextPosition = intersection.LastPoint + new XYZ(0, 0, 0.3);

                // Dodajemy oznaczenie "F" (FirstPoint) i "L" (LastPoint)
                CreateText3D(doc, firstTextPosition, $"{index}F");
                CreateText3D(doc, lastTextPosition, $"{index}L");

                index++;
            }
        }
    }
}