using RedisSharp.Contracts;
using RedisSharp.Factory;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Components
{
    public class ManagedLink<TDocument> : AsyncLink<TDocument>, IDeletable where TDocument : IAsyncModel
    {
        public ManagedLink(IAsyncModel document, [CallerMemberName] string linkName = "")
            : base(document, linkName)
        {
        }

        public override async Task SetAsync(string id)
        {
            await ClearAsync();
            await base.SetAsync(id);
        }

        public override async Task ClearAsync()
        {
            var currentId = await _links.GetByKeyAsync(_propertyName);

            if (!string.IsNullOrEmpty(currentId))
            {
                var currentDocument = ModelFactory.Create<TDocument>(currentId);
                await currentDocument.DeleteAsync();
            }

            await _links.RemoveAsync(_propertyName);
        }

        public async Task DeleteAsync()
        {
            await ClearAsync();
        }
    }
}
