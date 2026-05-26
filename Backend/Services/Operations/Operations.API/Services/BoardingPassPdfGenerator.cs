using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Operations.API.Services
{
    public class BoardingPassPdfGenerator
    {
        public static byte[] Generate(string pnr, string passengerName, string seatNo, string gate, string qrcode)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, pnr, passengerName, seatNo, gate, qrcode));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("SkyHorizon Airlines").SemiBold();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("BOARDING PASS").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("SkyHorizon").FontSize(14).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private static void ComposeContent(IContainer container, string pnr, string passengerName, string seatNo, string gate, string qrcode)
        {
            container.PaddingVertical(1, Unit.Centimetre).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Passenger: {passengerName}").Bold();
                    column.Item().Text($"PNR: {pnr}");
                    
                    column.Item().PaddingTop(10).Text($"Seat: {seatNo}").FontSize(16).Bold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"Gate: {gate}").FontSize(14);
                });

                row.ConstantItem(100).AlignRight().Column(col => 
                {
                    // For a real QR code you'd use a QR generator library.
                    // For QuestPDF we just simulate the QR data text for now, or use QuestPDF's extension
                    col.Item().Text("SECURE QR CODE").FontSize(8);
                    col.Item().Background(Colors.Grey.Lighten3).Padding(10).Text(qrcode.Substring(0, Math.Min(qrcode.Length, 15)) + "...").FontSize(8);
                });
            });
        }
    }
}
