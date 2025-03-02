using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SUN;

public class CreateTrianglesForFullySunlitPointsClass
{
    public static void CreateTrianglesForSunlitPoints(UIApplication uiapp, Document doc, XYZ analysisPoint, List<GetIntersections> wynik)
    {
        if (wynik.Count < 2)
        {
            TaskDialog.Show("Błąd", "Za mało punktów przecięcia do utworzenia trójkątów.");
            return;
        }

        TaskDialog.Show("Debug", $"Liczba punktów przed filtrowaniem: {wynik.Count * 2}");

        // 🔍 Filtrujemy TYLKO punkty nasłonecznione
        List<XYZ> widocznePunkty = new List<XYZ>();

        foreach (var w in wynik)
        {
            bool firstVisible = sunExtensions.IsPointExposedToSun(doc, analysisPoint, w.FirstPoint);
            bool lastVisible = sunExtensions.IsPointExposedToSun(doc, analysisPoint, w.LastPoint);

            TaskDialog.Show("Debug", $"Sprawdzam widoczność dla:\nFirst: {w.FirstPoint}, widoczny: {firstVisible}\nLast: {w.LastPoint}, widoczny: {lastVisible}");

            if (firstVisible) widocznePunkty.Add(w.FirstPoint);
            if (lastVisible) widocznePunkty.Add(w.LastPoint);
        }

        TaskDialog.Show("Debug", $"Liczba widocznych punktów po filtrowaniu: {widocznePunkty.Count}");


        // 🔄 Sortowanie punktów według kąta względem analysisPoint
        widocznePunkty.Sort((a, b) =>
        {
            double angleA = Math.Atan2(a.Y - analysisPoint.Y, a.X - analysisPoint.X);
            double angleB = Math.Atan2(b.Y - analysisPoint.Y, b.X - analysisPoint.X);
            return angleA.CompareTo(angleB);
        });

        TaskDialog.Show("Debug", "Punkty posortowane zgodnie z ruchem słońca.");

        // 🔄 Tworzenie trójkątów w parach (1,2), (3,4), (5,6)
        for (int i = 0; i < widocznePunkty.Count - 1; i += 2) // Skaczemy co dwa punkty
        {
            XYZ firstPoint = widocznePunkty[i];
            XYZ secondPoint = widocznePunkty[i + 1];

            // Sprawdzamy, czy punkty nie są współliniowe
            XYZ v1 = firstPoint - analysisPoint;
            XYZ v2 = secondPoint - analysisPoint;
            XYZ normal = v1.CrossProduct(v2);

            if (normal.IsZeroLength())
            {
                TaskDialog.Show("Błąd", "Punkty są współliniowe! Pomijam ten trójkąt.");
                continue;
            }

            TaskDialog.Show("Debug", $"Tworzę trójkąt:\nAnalysisPoint: {analysisPoint}\nFirst: {firstPoint}\nSecond: {secondPoint}");

            try
            {
                SolidCapCreation.CreateSolidCap(uiapp, analysisPoint, firstPoint, secondPoint);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Błąd", $"Nie udało się utworzyć trójkąta: {ex.Message}");
                continue;
            }
        }
    }



}







