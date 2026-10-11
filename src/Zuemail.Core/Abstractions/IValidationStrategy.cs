using System.Threading.Tasks;

namespace Zuemail.Core.Abstractions
{
    public interface IValidationStrategy<T>
    {
        Task<bool> ValidateAsync(T item);
    }
}
