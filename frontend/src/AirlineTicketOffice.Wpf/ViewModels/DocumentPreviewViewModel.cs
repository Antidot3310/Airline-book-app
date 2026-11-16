using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Documents;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления модального окна предпросмотра и печати документов W-10.
/// Обеспечивает отображение форматированного текстового макета с псевдографикой,
/// вывод на принтер через PrintDialog и экспорт в файл .txt.
/// </summary>
public partial class DocumentPreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Документ";

    [ObservableProperty]
    private string _documentNumber = string.Empty;

    [ObservableProperty]
    private string _createdAtText = string.Empty;

    [ObservableProperty]
    private string _formattedContent = string.Empty;

    /// <summary>
    /// Событие запроса закрытия окна предпросмотра.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Инициализирует модель представления данными сформированного документа.
    /// </summary>
    /// <param name="document">Сформированный документ с текстовым макетом.</param>
    public void Initialize(PrintDocumentDto document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        Title = string.IsNullOrWhiteSpace(document.Title) ? "Документ" : document.Title;
        DocumentNumber = document.DocumentNumber;
        CreatedAtText = document.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
        FormattedContent = document.FormattedContent;
    }

    /// <summary>
    /// Выполняет отправку текстового макета документа на печать через стандартный диалог Windows.
    /// </summary>
    [RelayCommand]
    public void Print()
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var flowDoc = new FlowDocument(new Paragraph(new Run(FormattedContent)))
                {
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 11,
                    PagePadding = new Thickness(40)
                };

                IDocumentPaginatorSource paginator = flowDoc;
                printDialog.PrintDocument(paginator.DocumentPaginator, Title);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при печати документа: {ex.Message}", "Печать", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Экспортирует текстовый макет документа в файл формата .txt.
    /// </summary>
    [RelayCommand]
    public void SaveToFile()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Сохранить документ в файл",
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"{CleanFileName(Title)}_{DocumentNumber}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, FormattedContent, Encoding.UTF8);
                MessageBox.Show("Документ успешно сохранён!", "Экспорт документа", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Закрывает окно предпросмотра.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Очищает имя файла от недопустимых символов файловой системы.
    /// </summary>
    private static string CleanFileName(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string clean = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return clean.Replace(' ', '_');
    }
}
