
using RedisSharp.Contracts;
using System.Runtime.CompilerServices;

namespace RedisSharp.Components
{
    public class BaseComponent : IModelComponent
    {
        protected readonly IAsyncModel _model;
        protected string _documentKey { get; set; }
        protected string _propertyName { get; set; }
        protected string _fullKey { get; set; }

        public BaseComponent(IAsyncModel document, [CallerMemberName] string propertyName = "")
        {
            _documentKey = document.GetKey();
            _model = document;

            _propertyName = propertyName;
            _fullKey = $"{_documentKey}:{_propertyName}";
        }
    }
}
