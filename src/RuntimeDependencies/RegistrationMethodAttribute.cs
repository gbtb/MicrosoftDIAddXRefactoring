namespace RuntimeDependencies;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RegistrationMethodAttribute: Attribute
{
}