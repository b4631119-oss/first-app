using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyFirstApp;

public partial class MainWindow : Window
{
    private string currentNumber = "";
    private double firstNumber = 0;
    private string operation = "";

    public MainWindow()
    {
        InitializeComponent();

        VersionText.Text = $"v{UpdateService.CurrentVersion}";

        AddHandler(Button.ClickEvent, Button_Click);
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
            return;

        string value = button.Tag?.ToString() ?? "";

        // C
        if (value == "C")
        {
            currentNumber = "";
            firstNumber = 0;
            operation = "";
            Result.Text = "0";
            return;
        }

        // Удалить последний символ
        if (value == "BACK")
        {
            if (currentNumber.Length > 0)
            {
                currentNumber = currentNumber[..^1];
                Result.Text = currentNumber == "" ? "0" : currentNumber;
            }

            return;
        }

        // %
        if (value == "%")
        {
            if (double.TryParse(currentNumber, out double number))
            {
                number /= 100;
                currentNumber = number.ToString();
                Result.Text = currentNumber;
            }

            return;
        }

        // +/-
        if (value == "PLUSMINUS")
        {
            if (currentNumber.StartsWith("-"))
                currentNumber = currentNumber[1..];
            else if (currentNumber != "")
                currentNumber = "-" + currentNumber;

            Result.Text = currentNumber == "" ? "0" : currentNumber;
            return;
        }

        // Цифры
        if (value.Length > 0 && char.IsDigit(value[0]))
        {
            currentNumber += value;
            Result.Text = currentNumber;
            return;
        }

        // Точка
        if (value == ".")
        {
            if (!currentNumber.Contains("."))
            {
                currentNumber += currentNumber == "" ? "0." : ".";
                Result.Text = currentNumber;
            }

            return;
        }

        // Операция
        if (value == "+" || value == "-" || value == "*" || value == "/")
        {
            if (double.TryParse(currentNumber, out double number))
            {
                firstNumber = number;
                operation = value;
                currentNumber = "";
            }

            return;
        }

        // =
        if (value == "=")
        {
            if (!double.TryParse(currentNumber, out double secondNumber))
                return;

            double result = operation switch
            {
                "+" => firstNumber + secondNumber,
                "-" => firstNumber - secondNumber,
                "*" => firstNumber * secondNumber,
                "/" => secondNumber == 0 ? 0 : firstNumber / secondNumber,
                _ => secondNumber
            };

            currentNumber = result.ToString();
            Result.Text = currentNumber;

            operation = "";
        }
    }
}