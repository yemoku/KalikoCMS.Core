﻿<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="EditPage.aspx.cs" Inherits="KalikoCMS.Admin.Content.EditPage" ValidateRequest="false" MasterPageFile="../Templates/MasterPages/Base.Master" %>
<%@ Register tagPrefix="cms" tagName="StringPropertyEditor" src="PropertyType/StringPropertyEditor.ascx" %>
<%@ Register tagPrefix="cms" tagName="UniversalDateTimePropertyEditor" src="PropertyType/UniversalDateTimePropertyEditor.ascx" %>
<%@ Register tagPrefix="cms" tagName="BooleanPropertyEditor" src="PropertyType/BooleanPropertyEditor.ascx" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
  <form id="MainForm" class="page-editor admin-page" role="form" runat="server">
    <div class="page-head">
      <h1><asp:Literal ID="PageHeader" runat="server" /></h1>
    </div>
    <div id="editor-panel">
      <div class="form-horizontal">
        <asp:Literal ID="Feedback" runat="server" />
        <fieldset>
          <legend>Standard information</legend>
          <cms:StringPropertyEditor ID="PageName" runat="server" />
          <cms:UniversalDateTimePropertyEditor ID="StartPublishDate" runat="server" />
          <cms:UniversalDateTimePropertyEditor ID="StopPublishDate" runat="server" />
          <cms:BooleanPropertyEditor ID="VisibleInMenu" runat="server" />
        </fieldset>
        <fieldset>
          <legend>Content</legend>
          <asp:Panel ID="EditControls" runat="server" />
          <div class="form-group">
            <label class="control-label col-xs-2" for="PageId">Page Id</label>
            <div class="controls col-xs-10">
              <span class="static-text">
                <asp:Literal ID="PageId" runat="server" /></span>
            </div>
          </div>
          <div class="form-actions">
            <asp:LinkButton runat="server" ID="SaveButton" CssClass="btn btn-lg btn-primary"><i class="icon-thumbs-up icon-white"></i> Save page</asp:LinkButton>
          </div>
        </fieldset>
      </div>
    </div>
  </form>
</asp:Content>


<asp:Content ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src="assets/js/kalikocms.admin.editor.min.js" type="text/javascript"></script>
    
    <script type="text/javascript">
      $(document).ready(function () {
        // TODO: Get editor options/toolbar from property attribute and web.config
        tinymce.init({
          skin_url: '../assets/vendors/tinymce/skins/lightgray',
          selector: "textarea.html-editor",
          plugins: [
              "advlist autolink lists link image charmap anchor",
              "searchreplace visualblocks code",
              "insertdatetime media table contextmenu paste"
          ],
          resize: true,
          height: 300,
          menubar: false,
          extended_valid_elements: "i[class],span",
          relative_urls: false,
          toolbar: "undo redo | styleselect | bold italic | alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | link image | code",
          file_picker_callback: function (callback, value, meta) {
            if (meta.filetype == 'file') {
              top.registerCallback(function (newUrl, newType) { callback(newUrl); });
              top.propertyEditor.dialogs.openSelectLinkDialog(value, 0);
            }
            if (meta.filetype == 'image') {
              top.registerCallback(function (imagePath, cropX, cropY, cropW, cropH, originalPath, description) { callback(imagePath, { alt: description }); });
              top.propertyEditor.dialogs.openEditImageDialog(value, value, '', '', '', '', '', '', '');
            }
          }
        });

        warnBeforeLeavingIfChangesBeenMade();
      });
    </script>
</asp:Content>
