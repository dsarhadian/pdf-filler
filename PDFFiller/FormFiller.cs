using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using PDFFiller.Models;

namespace PDFFiller
{
    class FormFiller
    {
        private FileInfo pdfFile;
        private List<FormField>? formFields;

        public FormFiller(FileInfo pdfFile, string jsonData)
        {
            this.pdfFile = pdfFile;
            this.formFields = JsonConvert.DeserializeObject<List<FormField>>(jsonData);
        }

        public bool FillForm()
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    PdfReader pdfReader = new PdfReader(this.pdfFile.FullName);
                    PdfStamper pdfStamper = new PdfStamper(pdfReader, memoryStream);
                    AcroFields acroFields = pdfStamper.AcroFields;

                    foreach (var formField in formFields)
                    {
                        int typeInt = acroFields.GetFieldType(formField.Name);

                        if ((FieldType)typeInt == FieldType.FIELD_TYPE_PUSHBUTTON)
                        {
                            Regex regex = new Regex(
                                @"^data:image/(?<mediaType>[^;]+);base64,(?<data>.*)"
                            );
                            Match match = regex.Match(formField.Value);

                            if (match.Length == 0)
                            {
                                acroFields.SetField(formField.Name, formField.Value);
                            }
                            else
                            {
                                // acroFields.SetField(formField.Name, match.Groups["data"].Value);
                                var fieldPosition = acroFields.GetFieldPositions(formField.Name);
                                Rectangle rect = new Rectangle(
                                    fieldPosition[1],
                                    fieldPosition[2],
                                    fieldPosition[3],
                                    fieldPosition[4]
                                );
                                Image image = Image.GetInstance(
                                    Convert.FromBase64String(match.Groups["data"].Value)
                                );

                                image.ScaleToFit(rect.Width, 65);

                                image.SetAbsolutePosition(rect.Left, (rect.Bottom - rect.Height));
                                pdfStamper.GetOverContent((int)fieldPosition[0]).AddImage(image);
                            }
                        }
                        else
                        {
                            acroFields.SetField(formField.Name, formField.Value);
                        }
                    }

                    pdfStamper.FormFlattening = true;
                    pdfStamper.Close();

                    byte[] pdfOutput = memoryStream.ToArray();

                    using var stdout = Console.OpenStandardOutput();
                    stdout.Write(pdfOutput);
                    stdout.Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"An error ocurred while trying to file the PDF: {ex.Message}"
                );
                Console.Error.Close();

                return false;
            }
        }
    }
}
