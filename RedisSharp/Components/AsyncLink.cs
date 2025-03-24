using RedisSharp.Components;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RedisSharp
{
    public class AsyncLink<TDocument> : BaseComponent where TDocument : IAsyncModel
    {
        protected AsyncDictionary<string, string> _links { get; set; }

        public AsyncLink(IAsyncModel document, [CallerMemberName] string propertyName = "") : base(document, propertyName) 
        {
            _links = new AsyncDictionary<string, string>(document, "links");
        }

        public virtual async Task SetAsync(string id)
        {
            await _links.SetAsync(_propertyName, id);
        }

        public virtual async Task SetAsync(TDocument document)
        {
            if (document == null)
            {
                await ClearAsync();
                return;
            }

            await SetAsync(document.Id);
        }

        public virtual async Task ClearAsync() 
        {
            await _links.RemoveAsync(_propertyName);
        }

        public virtual async Task<string> GetIdAsync()
        {
            return await _links.GetByKeyAsync(_propertyName);
        }

        public virtual async Task<TDocument> GetAsync()
        {
            return await RedisRepository.LoadAsync<TDocument>(await GetIdAsync());
        }
    }
}
