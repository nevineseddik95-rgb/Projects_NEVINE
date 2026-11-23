using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProductStore.Api.Hubs;

[Authorize(Roles = "Owner,Viewer")]
public class AdminHub : Hub { }