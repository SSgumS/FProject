﻿using BlazorFluentUI;
using FProject.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Timers;

namespace FProject.Client.Pages
{
    public partial class Writepad
    {
        [Inject]
        IJSRuntime JS { get; set; }
        [Inject]
        HttpClient Http { get; set; }
        [Inject]
        NavigationManager Navigation { get; set; }

        [Parameter]
        public int Id { get; set; }
        [Parameter]
        public bool AdminReview { get; set; }

        bool IsInitiationDone { get; set; }
        WritepadPanel PanelRef { get; set; }
        float PadRatio { get; set; } = 0.7f;
        bool PanelCollapsed { get; set; }
        bool InitiationDone { get; set; }
        string WritepadCompressedJson { get; set; }
        public WritepadDTO WritepadInstance { get; set; }
        public bool AutoSaveChecked { get; set; }
        public bool IsSaving { get; set; }
        bool ForceNotRender { get; set; }
        public IJSObjectReference JSRef { get; set; }
        public Timer SaveTimer { get; set; } = new Timer(30000);

        private DotNetObjectReference<Writepad> componentRef;

        protected override async Task OnInitializedAsync()
        {
            componentRef = DotNetObjectReference.Create(this);

            JSRef = await JS.InvokeAsync<IJSObjectReference>("ImportGlobal", "Writepad", "/ts/Pages/Writepad/Writepad.razor.js");

            SaveTimer.Elapsed += SaveTimerElapsedHandler;
        }

        protected override async Task OnParametersSetAsync()
        {
            var query = new Uri(Navigation.Uri).Query;
            foreach (var queryItem in QueryHelpers.ParseQuery(query))
            {
                switch (queryItem.Key)
                {
                    case "adminreview":
                        AdminReview = true;
                        break;
                }
            }

            var taskWritepadInstance = Http.GetFromJsonAsync<WritepadDTO>($"api/Writepad/{Id}?admin={AdminReview}");
            var taskWritepadCompressedJson = Http.GetStringAsync($"api/Writepad/{Id}?withPoints=true&admin={AdminReview}");
            await Task.WhenAll(taskWritepadInstance, taskWritepadCompressedJson);
            WritepadInstance = taskWritepadInstance.Result;
            WritepadCompressedJson = taskWritepadCompressedJson.Result;

            if (WritepadInstance.Type == WritepadType.WordGroup)
            {
                WritepadInstance.Text.Content = WritepadInstance.Text.Content.Replace(" ", "<br />");
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender || InitiationDone)
            {
                return;
            }

            var currentTime = await Http.GetFromJsonAsync<long>($"api/Writepad/CurrentTime");
            await JSRef.InvokeVoidAsync("init", componentRef, PadRatio, currentTime - 1616060000000, WritepadCompressedJson);
            InitiationDone = true;
        }

        protected override bool ShouldRender()
        {
            return WritepadInstance is not null;
        }

        public void SaveTimerElapsedHandler(object s = default, ElapsedEventArgs e = default)
        {
            if (!IsSaving)
            {
                JSRef.InvokeVoidAsync("save");
            }
        }

        [JSInvokable]
        public async Task<SaveResponseDTO> Save(string savePointsDTOCompressedJson)
        //public async Task<SaveResponseDTO> Save(DateTimeOffset lastModified, DrawingPoint[] drawingPoints, DeletedDrawing[] deletedDrawings)
        {
            IsSaving = true;
            PanelRef.StateHasChangedPublic();
            try
            {
                //var response = await Http.PostAsJsonAsync($"api/Writepad/{Id}", new SavePointsDTO
                //{
                //    LastModified = lastModified,
                //    NewPoints = drawingPoints,
                //    DeletedDrawings = deletedDrawings
                //});
                //var stringContent = new StringContent(savePointsDTOCompressedJson, Encoding.ASCII, "application/json");
                //var stringContent = new StringContent(savePointsDTOJson, Encoding.UTF8, "application/json");
                var response = await Http.PostAsJsonAsync($"api/Writepad/{Id}", savePointsDTOCompressedJson);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var saveResponse = await response.Content.ReadFromJsonAsync<SavePointsResponseDTO>();
                        WritepadInstance.LastModified = saveResponse.LastModified;
                        break;
                    case HttpStatusCode.BadRequest:
                        var error = await response.Content.ReadFromJsonAsync<WritepadEditionError>();
                        if (error == WritepadEditionError.SignNotAllowed)
                        {
                            PanelRef.NotAllowedDialogOpen = true;
                        }
                        break;
                }

                return new SaveResponseDTO
                {
                    StatusCode = response.StatusCode,
                    LastModified = WritepadInstance.LastModified
                };
            }
            catch (AccessTokenNotAvailableException)
            {
                return new SaveResponseDTO
                {
                    StatusCode = HttpStatusCode.Unauthorized
                };
            }
        }

        [JSInvokable]
        public void ReleaseSaveToken()
        {
            IsSaving = false;
            PanelRef.StateHasChangedPublic();
        }

        [JSInvokable]
        public void UndoRedoUpdator(bool undo, bool redo)
        {
            PanelRef.UndoRedoUpdator(undo, redo);
        }

        public class SaveResponseDTO
        {
            public HttpStatusCode StatusCode { get; set; }
            public DateTimeOffset LastModified { get; set; }
        }
    }
}
