/// <summary>Paso que lleva la cuenta de errores (por ejemplo, conexiones en un lugar equivocado) para los resultados.</summary>
public interface IStepErrors
{
    int Errors { get; }
}
