using System;

namespace MicrosoftDI.AddXRefactoring.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RegistrationMethodAttribute: Attribute
{
}