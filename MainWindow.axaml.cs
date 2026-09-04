using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MyFirstApp;

public partial class MainWindow : Window
{
    private string _currentNumber = "";
    private double _firstNumber = 0;
    private string _operation = "";
    private bool _operationPerformed = false;

    public MainWindow()
    {
        InitializeComponent();

        VersionText.Text = $"v{UpdateService.CurrentVersion}";

        AddHandler(Button.ClickEvent, Button_Click);
        AddHandler(InputElement.KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClearAll();
            return;
        }

        if (e.Key == Key.Back)
        {
            Backspace();
            return;
        }

        if (e.Key == Key.Enter)
        {
            Calculate();
            return;
        }

        var keyChar = GetKeyChar(e.Key);
        if (keyChar.HasValue)
        {
            HandleInput(keyChar.Value.ToString());
            e.Handled = true;
        }
    }

    private static char? GetKeyChar(Key key)
    {
        return key switch
        {
            Key.D0 or Key.NumPad0 => '0',
            Key.D1 or Key.NumPad1 => '1',
            Key.D2 or Key.NumPad2 => '2',
            Key.D3 or Key.NumPad3 => '3',
            Key.D4 or Key.NumPad4 => '4',
            Key.D5 or Key.NumPad5 => '5',
            Key.D6 or Key.NumPad6 => '6',
            Key.D7 or Key.NumPad7 => '7',
            Key.D8 or Key.NumPad8 => '8',
            Key.D9 or Key.NumPad9 => '9',
            Key.OemPeriod or Key.Decimal => '.',
            Key.OemPlus or Key.Add => '+',
            Key.OemMinus or Key.Subtract => '-',
            Key.Multiply => '*',
            Key.Divide => '/',
            Key.OemQuestion or Key.Oem5 => '%',
            _ => null
        };
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
            return;

        var value = button.Tag?.ToString() ?? "";
        HandleInput(value);
    }

    private void HandleInput(string value)
    {
        if (value == "C")
        {
            ClearAll();
            return;
        }

        if (value == "BACK")
        {
            Backspace();
            return;
        }

        if (value == "%")
        {
            Percent();
            return;
        }

        if (value == "PLUSMINUS")
        {
            ToggleSign();
            return;
        }

        if (value.Length == 1 && char.IsDigit(value[0]))
        {
            AppendDigit(value[0]);
            return;
        }

        if (value == ".")
        {
            AppendDecimal();
            return;
        }

        if (value is "+" or "-" or "*" or "/")
        {
            SetOperation(value);
            return;
        }

        if (value == "=")
        {
            Calculate();
        }
    }

    private void ClearAll()
    {
        _currentNumber = "";
        _firstNumber = 0;
        _operation = "";
        _operationPerformed = false;
        Result.Text = "0";
    }

    private void Backspace()
    {
        if (_currentNumber.Length > 0)
        {
            _currentNumber = _currentNumber[..^1];
            Result.Text = string.IsNullOrEmpty(_currentNumber) ? "0" : _currentNumber;
        }
    }

    private void AppendDigit(char digit)
    {
        if (_operationPerformed)
        {
            _currentNumber = "";
            _operationPerformed = false;
        }

        if (_currentNumber.Length >= 15)
            return;

        if (_currentNumber == "0")
        {
            _currentNumber = digit.ToString();
        }
        else
        {
            _currentNumber += digit;
        }

        Result.Text = _currentNumber;
    }

    private void AppendDecimal()
    {
        if (_operationPerformed)
        {
            _currentNumber = "";
            _operationPerformed = false;
        }

        if (!_currentNumber.Contains("."))
        {
            _currentNumber += _currentNumber == "" ? "0." : ".";
            Result.Text = _currentNumber;
        }
    }

    private void ToggleSign()
    {
        if (string.IsNullOrEmpty(_currentNumber) || _currentNumber == "0")
            return;

        if (_currentNumber.StartsWith("-"))
        {
            _currentNumber = _currentNumber[1..];
        }
        else
        {
            _currentNumber = "-" + _currentNumber;
        }

        Result.Text = _currentNumber;
    }

    private void Percent()
    {
        if (double.TryParse(_currentNumber, out var number))
        {
            number /= 100;
            _currentNumber = FormatNumber(number);
            Result.Text = _currentNumber;
        }
    }

    private void SetOperation(string op)
    {
        if (!double.TryParse(_currentNumber, out _firstNumber))
        {
            _firstNumber = 0;
        }

        if (!_operationPerformed && !string.IsNullOrEmpty(_operation))
        {
            Calculate();
            if (!double.TryParse(_currentNumber, out _firstNumber))
            {
                _firstNumber = 0;
            }
        }

        _operation = op;
        _operationPerformed = true;
    }

    private void Calculate()
    {
        if (!double.TryParse(_currentNumber, out var secondNumber))
        {
            ShowError("Ошибка");
            return;
        }

        double result;

        try
        {
            result = _operation switch
            {
                "+" => _firstNumber + secondNumber,
                "-" => _firstNumber - secondNumber,
                "*" => _firstNumber * secondNumber,
                "/" => secondNumber == 0 ? throw new System.DivideByZeroException() : _firstNumber / secondNumber,
                _ => secondNumber
            };

            if (double.IsInfinity(result) || double.IsNaN(result))
            {
                throw new System.OverflowException();
            }
        }
        catch (System.DivideByZeroException)
        {
            ShowError("Деление на ноль");
            return;
        }
        catch (System.OverflowException)
        {
            ShowError("Переполнение");
            return;
        }

        _currentNumber = FormatNumber(result);
        Result.Text = _currentNumber;

        _firstNumber = result;
        _operation = "";
        _operationPerformed = true;
    }

    private static string FormatNumber(double value)
    {
        if (value == (long)value && System.Math.Abs(value) < 1e15)
        {
            return ((long)value).ToString();
        }

        var str = value.ToString("G15");
        if (str.Length > 15)
        {
            str = value.ToString("E6");
        }
        return str;
    }

    private void ShowError(string message)
    {
        Result.Text = message;
        _currentNumber = "";
        _firstNumber = 0;
        _operation = "";
        _operationPerformed = false;
    }
}