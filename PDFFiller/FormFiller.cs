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

                    BaseFont baseFont = BaseFont.CreateFont(
                        BaseFont.TIMES_ROMAN,
                        BaseFont.WINANSI,
                        BaseFont.NOT_EMBEDDED
                    );

                    if (formFields == null)
                    {
                        return false;
                    }

                    foreach (var formField in formFields)
                    {
                        if (formField.Value == null)
                        {
                            continue;
                        }

                        int typeInt = acroFields.GetFieldType(formField.Name);

                        if (
                            (FieldType)typeInt == FieldType.FIELD_TYPE_PUSHBUTTON
                            || (FieldType)typeInt == FieldType.FIELD_TYPE_SIGNATURE
                        )
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

                                image.ScaleToFit(rect.Width, rect.Height);

                                image.SetAbsolutePosition(rect.Left, rect.Bottom);
                                pdfStamper.GetOverContent((int)fieldPosition[0]).AddImage(image);
                            }
                        }
                        else if (
                            (FieldType)typeInt == FieldType.FIELD_TYPE_TEXT
                            && !formField.Value.Contains('\n')
                        )
                        {
                            var fieldPositions = acroFields.GetFieldPositions(formField.Name);
                            if (fieldPositions != null && fieldPositions.Length == 5)
                            {
                                int page = (int)fieldPositions[0];
                                float left = fieldPositions[1];
                                float bottom = fieldPositions[2];
                                float right = fieldPositions[3];
                                float top = fieldPositions[4];

                                float fontSize = formField.FontSize ?? 10.0f;

                                PdfContentByte cb = pdfStamper.GetOverContent(page);
                                cb.BeginText();
                                cb.SetFontAndSize(baseFont, fontSize);

                                float x = left;
                                float fieldHeight = top - bottom;
                                float y = bottom + (fieldHeight - fontSize + 2) / 2;

                                cb.ShowTextAligned(
                                    PdfContentByte.ALIGN_LEFT,
                                    formField.Value,
                                    x,
                                    y,
                                    0
                                );
                                cb.EndText();
                            }

                            // Remove the actual form field
                            acroFields.RemoveField(formField.Name);
                        }
                        else
                        {
                            acroFields.SetFieldProperty(
                                formField.Name,
                                "textsize",
                                formField.FontSize ?? 10.0f,
                                null
                            );
                            acroFields.SetFieldProperty(formField.Name, "textfont", baseFont, null);
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
