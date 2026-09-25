using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FocusLens.App.Controls;

/// <summary>
/// An editable ComboBox that filters its list as the user types (case-insensitive "contains").
/// Free text is allowed, so a model that is not in the list yet can still be entered.
/// </summary>
public class SearchableComboBox : ComboBox
{
    private TextBox? _editor;
    private bool _suppress;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_editor is not null) _editor.TextChanged -= OnTextChanged;
        _editor = GetTemplateChild("PART_EditableTextBox") as TextBox;
        if (_editor is not null) _editor.TextChanged += OnTextChanged;
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        ClearFilter();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key is Key.Down or Key.Up && !IsDropDownOpen) IsDropDownOpen = true;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress || _editor is null || !_editor.IsKeyboardFocusWithin) return;

        var text = _editor.Text;
        if (CollectionViewSource.GetDefaultView(ItemsSource) is not ICollectionView view) return;

        view.Filter = string.IsNullOrWhiteSpace(text)
            ? null
            : item => item?.ToString()?.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) == true;

        if (!IsDropDownOpen) IsDropDownOpen = true;
        // Opening the popup selects all text in some themes; put the caret back so typing continues smoothly.
        _suppress = true;
        _editor.CaretIndex = _editor.Text.Length;
        _suppress = false;
    }

    private void ClearFilter()
    {
        if (CollectionViewSource.GetDefaultView(ItemsSource) is ICollectionView view) view.Filter = null;
    }
}
