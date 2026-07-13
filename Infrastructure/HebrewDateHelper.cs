using System.Globalization;

namespace WeddingOrchestrator.Api.Infrastructure;

public static class HebrewDateHelper
{
    private static readonly string[] NonLeapMonthNames =
    {
        "", "Tishrei", "Cheshvan", "Kislev", "Tevet", "Sh'vat", "Adar",
        "Nisan", "Iyyar", "Sivan", "Tamuz", "Av", "Elul",
    };

    private static readonly string[] LeapMonthNames =
    {
        "", "Tishrei", "Cheshvan", "Kislev", "Tevet", "Sh'vat", "Adar I", "Adar II",
        "Nisan", "Iyyar", "Sivan", "Tamuz", "Av", "Elul",
    };

    public static (int Day, string MonthName, int Year) GetHebrewParts(DateTime date)
    {
        var calendar = new HebrewCalendar();
        var day = calendar.GetDayOfMonth(date);
        var month = calendar.GetMonth(date);
        var year = calendar.GetYear(date);
        var monthNames = calendar.IsLeapYear(year) ? LeapMonthNames : NonLeapMonthNames;

        return (day, monthNames[month], year);
    }
}
