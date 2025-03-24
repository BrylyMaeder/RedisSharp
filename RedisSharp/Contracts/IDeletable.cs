using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Contracts
{
    public interface IDeletable
    {
        Task DeleteAsync();
    }
}
