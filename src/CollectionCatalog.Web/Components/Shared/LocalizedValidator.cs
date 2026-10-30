using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CollectionCatalog.Web.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Components.Shared;

/// <summary>
/// Replacement of <see cref="DataAnnotationsValidator"/> with translated messages. Attributes keep the English message
/// template (<c>"The name can have at most {1} characters."</c>), which is the resource key; the template is translated
/// first and only then formatted with the attribute's arguments. Messages of <see cref="IValidatableObject"/> are keys too.
/// </summary>
public sealed class LocalizedValidator : ComponentBase, IDisposable
{
    private EditContext? _editContext;
    private ValidationMessageStore? _messages;

    [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

    [Inject] private IStringLocalizer<AppText> T { get; set; } = null!;

    [Inject] private IServiceProvider Services { get; set; } = null!;

    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException($"{nameof(LocalizedValidator)} must be placed inside an EditForm.");
        }
        if (ReferenceEquals(CurrentEditContext, _editContext))
        {
            return;
        }

        Detach();
        _editContext = CurrentEditContext;
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnValidationRequested += ValidationRequested;
        _editContext.OnFieldChanged += FieldChanged;
    }

    public void Dispose() => Detach();

    private void Detach()
    {
        if (_editContext is null)
        {
            return;
        }
        _editContext.OnValidationRequested -= ValidationRequested;
        _editContext.OnFieldChanged -= FieldChanged;
        _messages?.Clear();
        _editContext = null;
    }

    private void ValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        var model = _editContext!.Model;
        _messages!.Clear();
        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            ValidateProperty(new FieldIdentifier(model, property.Name), property);
        }

        if (model is IValidatableObject validatable)
        {
            foreach (var result in validatable.Validate(new ValidationContext(model, Services, items: null)))
            {
                var message = T[result.ErrorMessage ?? ""].Value;
                var members = result.MemberNames.Any() ? result.MemberNames : [""];
                foreach (var member in members)
                {
                    _messages.Add(new FieldIdentifier(model, member), message);
                }
            }
        }

        _editContext.NotifyValidationStateChanged();
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs e)
    {
        var field = e.FieldIdentifier;
        if (field.Model.GetType().GetProperty(field.FieldName) is not { } property)
        {
            return;
        }
        _messages!.Clear(field);
        ValidateProperty(field, property);
        _editContext!.NotifyValidationStateChanged();
    }

    private void ValidateProperty(FieldIdentifier field, PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0)
        {
            return;
        }

        var value = property.GetValue(field.Model);
        var context = new ValidationContext(field.Model, Services, items: null) { MemberName = property.Name, DisplayName = property.Name };
        foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>())
        {
            if (attribute.GetValidationResult(value, context) is not null)
            {
                _messages!.Add(field, Message(attribute, property.Name));
            }
        }
    }

    /// <summary>Translated template formatted with the same arguments the attribute itself would use.</summary>
    private string Message(ValidationAttribute attribute, string name)
    {
        var template = T[attribute.ErrorMessage ?? "The value is not valid."].Value;
        object?[] arguments = attribute switch
        {
            StringLengthAttribute length => [name, length.MaximumLength, length.MinimumLength],
            RangeAttribute range => [name, range.Minimum, range.Maximum],
            MaxLengthAttribute max => [name, max.Length],
            MinLengthAttribute min => [name, min.Length],
            _ => [name],
        };
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, arguments);
    }
}
