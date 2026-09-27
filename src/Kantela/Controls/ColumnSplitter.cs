using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Kantela.Controls;

// Resizes the column on its left when dragged. Must be placed directly in a Grid.
public sealed partial class ColumnSplitter : Grid
{
    private ColumnDefinition? _target;
    private double _startX;
    private double _startWidth;

    public ColumnSplitter()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _target = null;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        int column = GetColumn(this);
        if (Parent is not Grid grid || column == 0)
        {
            return;
        }

        _target = grid.ColumnDefinitions[column - 1];
        _startX = e.GetCurrentPoint(grid).Position.X;
        _startWidth = _target.ActualWidth;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_target is null || Parent is not Grid grid)
        {
            return;
        }

        double width = _startWidth + e.GetCurrentPoint(grid).Position.X - _startX;
        _target.Width = new GridLength(Math.Clamp(width, _target.MinWidth, _target.MaxWidth));
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _target = null;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
