
using System.Windows;
namespace PalworldManager.Services.Infrastructure;
public interface IDialogService{bool Confirm(string message,string title);void Info(string message,string title);}
public sealed class DialogService:IDialogService{
 public bool Confirm(string m,string t)=>MessageBox.Show(m,t,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
 public void Info(string m,string t)=>MessageBox.Show(m,t,MessageBoxButton.OK,MessageBoxImage.Information);
}
