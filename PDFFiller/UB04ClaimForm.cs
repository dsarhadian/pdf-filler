using iTextSharp.text;
using iTextSharp.text.pdf;
using PDFFiller.Models;

namespace PDFFiller
{
    class UB04ClaimForm
    {
        private FileInfo pdfFile;
        private Dictionary<int, List<FormField>>? formFields;

        public UB04ClaimForm(string formDataJson)
        {
            this.pdfFile = new FileInfo("Data/ub04.pdf");
            this.formFields = FormFieldParser.ParseJson(formDataJson);
        }

        public bool FillForm()
        {
            try
            {
                if (this.formFields == null)
                {
                    Console.Error.WriteLine("No form fields provided.");
                    Console.Error.Close();

                    return false;
                }

                using MemoryStream finalOutputStream = new MemoryStream();
                Document document = new Document();
                PdfCopy pdfCopy = new PdfCopy(document, finalOutputStream);

                document.Open();

                foreach (var pageEntry in this.formFields)
                {
                    using MemoryStream stampedPageStream = new MemoryStream();
                    PdfReader pdfReader = new PdfReader(this.pdfFile.FullName);
                    PdfStamper pdfStamper = new PdfStamper(pdfReader, stampedPageStream);
                    AcroFields acroFields = pdfStamper.AcroFields;

                    foreach (FormField field in pageEntry.Value)
                    {
                        acroFields.SetField(field.Name, field.Value);
                    }

                    pdfStamper.FormFlattening = true;
                    pdfStamper.Close();
                    pdfReader.Close();

                    // Now, re-read the stamped page from the temporary stream.
                    PdfReader stampedReader = new PdfReader(stampedPageStream.ToArray());
                    // Import the page (assuming the original PDF is single-paged).
                    PdfImportedPage importedPage = pdfCopy.GetImportedPage(stampedReader, 1);
                    pdfCopy.AddPage(importedPage);

                    stampedReader.Close();
                }

                document.Close();
                pdfCopy.Close();

                byte[] pdfOutput = finalOutputStream.ToArray();

                using var stdout = Console.OpenStandardOutput();
                stdout.Write(pdfOutput);
                stdout.Flush();

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"An error ocurred while trying to fill the PDF: {ex.Message}"
                );
                Console.Error.Close();

                return false;
            }
        }
    }
}
