
using System;
using System.Threading;
using System.Threading.Tasks;
namespace PalworldManager.Services.Infrastructure;
public sealed class TransactionManager {
 public async Task ExecuteAsync(Func<CancellationToken,Task> action,CancellationToken token=default){
   await action(token);
 }
}
