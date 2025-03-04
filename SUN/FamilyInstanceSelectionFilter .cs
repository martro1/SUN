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
    public class FamilyInstanceSelectionFilter : ISelectionFilter
    {
        private readonly string _familyName;

        public FamilyInstanceSelectionFilter(string familyName)
        {
            _familyName = familyName;
        }

        public bool AllowElement(Element elem)
        {
            // Sprawdzenie, czy element to FamilyInstance i czy ma w³aœciw¹ nazwê rodziny
            if (elem is FamilyInstance fi && fi.Symbol.Family.Name == _familyName)
            {
                return true;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // Nie pozwalamy na wybór geometrii, tylko ca³ych elementów
        }


    }


}