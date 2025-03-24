using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Contracts
{
    public interface IDeletionListener
    {
        /// <summary>
        /// Handle deletion of internal links
        /// </summary>
        /// <returns></returns>
        Task OnDeleted();
    }
}
