using System.Text;
using System.Text.Json;
using Altinn.App.Core.Configuration;
using Altinn.App.Core.Helpers;
using Altinn.App.Core.Interface;
using Altinn.App.Core.Internal.App;
using Altinn.App.Core.Models;
using Altinn.App.Core.Models.Layout;
using Altinn.App.Core.Models.Layout.Components;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Altinn.App.Core.Implementation
{
    /// <summary>
    /// App implementation of the execution service needed for executing an Altinn Core Application (Functional term).
    /// </summary>
    public class AppResourcesSI : IAppResources
    {
        private readonly AppSettings _settings;
        private readonly IAppMetadata _appMetadata;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger _logger;

        private static readonly JsonSerializerOptions DESERIALIZER_OPTIONS = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AppResourcesSI"/> class.
        /// </summary>
        /// <param name="settings">The app repository settings.</param>
        /// <param name="appMetadata">App metadata service</param>
        /// <param name="hostingEnvironment">The hosting environment</param>
        /// <param name="logger">A logger from the built in logger factory.</param>
        public AppResourcesSI(
            IOptions<AppSettings> settings,
            IAppMetadata appMetadata,
            IWebHostEnvironment hostingEnvironment,
            ILogger<AppResourcesSI> logger)
        {
            _settings = settings.Value;
            _appMetadata = appMetadata;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        /// <inheritdoc/>
        public byte[] GetAppResource(string org, string app, string resource)
        {
            byte[] fileContent;

            if (resource == _settings.RuleHandlerFileName)
            {
                fileContent = ReadFileContentsFromLegalPath(_settings.AppBasePath + _settings.UiFolder, resource);
            }
            else if (resource == _settings.FormLayoutJSONFileName)
            {
                fileContent = ReadFileContentsFromLegalPath(_settings.AppBasePath + _settings.UiFolder, resource);
            }
            else if (resource == _settings.RuleConfigurationJSONFileName)
            {
                fileContent = ReadFileContentsFromLegalPath(_settings.AppBasePath + _settings.UiFolder, resource);
            }
            else
            {
                fileContent = ReadFileContentsFromLegalPath(_settings.AppBasePath + _settings.GetResourceFolder(), resource);
            }

            return fileContent;
        }

        /// <inheritdoc />
        public byte[] GetText(string org, string app, string textResource)
        {
            return ReadFileContentsFromLegalPath(_settings.AppBasePath + _settings.ConfigurationFolder + _settings.TextFolder, textResource);
        }

        /// <inheritdoc />
        public async Task<TextResource?> GetTexts(string org, string app, string language)
        {
            string pathTextsFolder = _settings.AppBasePath + _settings.ConfigurationFolder + _settings.TextFolder;
            string fullFileName = Path.Join(pathTextsFolder, $"resource.{language}.json");

            PathHelper.EnsureLegalPath(pathTextsFolder, fullFileName);

            if (!File.Exists(fullFileName))
            {
                return null;
            }

            using (FileStream fileStream = new(fullFileName, FileMode.Open, FileAccess.Read))
            {
                TextResource textResource = (await System.Text.Json.JsonSerializer.DeserializeAsync<TextResource>(fileStream, DESERIALIZER_OPTIONS))!;
                textResource.Id = $"{org}-{app}-{language}";
                textResource.Org = org;
                textResource.Language = language;

                return textResource;
            }
        }

        /// <inheritdoc />
        public Application GetApplication()
        {
            try
            {
                ApplicationMetadata applicationMetadata = _appMetadata.GetApplicationMetadata().Result;
                Application application = applicationMetadata;
                if (applicationMetadata.OnEntry != null)
                {
                    application.OnEntry = new OnEntryConfig()
                    {
                        Show = applicationMetadata.OnEntry.Show
                    };
                }
                
                return application;
            }
            catch (AggregateException ex)
            {
                throw new ApplicationConfigException("Failed to read application metadata", ex.InnerException ?? ex);
            }
        }

        /// <inheritdoc/>
        public string? GetApplicationXACMLPolicy()
        {
            try
            {
                return _appMetadata.GetApplicationXACMLPolicy().Result;
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "Something went wrong fetching application policy");
                return null;
            }
        }

        /// <inheritdoc/>
        public string? GetApplicationBPMNProcess()
        {
            try
            {
                return _appMetadata.GetApplicationBPMNProcess().Result;
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "Something went wrong fetching application policy");
                return null;
            }
        }

        /// <inheritdoc/>
        public string GetModelMetaDataJSON(string org, string app)
        {
            Application applicationMetadata = GetApplication();

            string dataTypeId = string.Empty;
            foreach (DataType data in applicationMetadata.DataTypes)
            {
                if (data.AppLogic != null && !string.IsNullOrEmpty(data.AppLogic.ClassRef))
                {
                    dataTypeId = data.Id;
                }
            }

            string filename = _settings.AppBasePath + _settings.ModelsFolder + dataTypeId + "." + _settings.ServiceMetadataFileName;
            string filedata = File.ReadAllText(filename, Encoding.UTF8);

            return filedata;
        }

        /// <inheritdoc/>
        public string GetModelJsonSchema(string modelId)
        {
            string legalPath = $"{_settings.AppBasePath}{_settings.ModelsFolder}";
            string filename = $"{legalPath}{modelId}.{_settings.JsonSchemaFileName}";
            PathHelper.EnsureLegalPath(legalPath, filename);

            string filedata = File.ReadAllText(filename, Encoding.UTF8);

            return filedata;
        }

        /// <inheritdoc/>
        public byte[]? GetRuntimeResource(string resource)
        {
            byte[]? fileContent = null;
            string path;
            if (resource == _settings.RuntimeAppFileName)
            {
                path = Path.Combine(_hostingEnvironment.WebRootPath, "runtime", "js", "react", _settings.RuntimeAppFileName);
            }
            else if (resource == _settings.ServiceStylesConfigFileName)
            {
                return Encoding.UTF8.GetBytes(_settings.GetStylesConfig());
            }
            else
            {
                path = Path.Combine(_hostingEnvironment.WebRootPath, "runtime", "css", "react", _settings.RuntimeCssFileName);
            }

            if (File.Exists(path))
            {
                fileContent = File.ReadAllBytes(path);
            }

            return fileContent;
        }

        /// <inheritdoc />
        public string? GetPrefillJson(string dataModelName = "ServiceModel")
        {
            string legalPath = _settings.AppBasePath + _settings.ModelsFolder;
            string filename = legalPath + dataModelName + ".prefill.json";
            PathHelper.EnsureLegalPath(legalPath, filename);

            string? filedata = null;
            if (File.Exists(filename))
            {
                filedata = File.ReadAllText(filename, Encoding.UTF8);
            }

            return filedata;
        }

        /// <inheritdoc />
        public string? GetLayoutSettingsString()
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, _settings.FormLayoutSettingsFileName);
            string? filedata = null;
            if (File.Exists(filename))
            {
                filedata = File.ReadAllText(filename, Encoding.UTF8);
            }

            return filedata;
        }

        /// <inheritdoc />
        public LayoutSettings GetLayoutSettings()
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, _settings.FormLayoutSettingsFileName);
            if (File.Exists(filename))
            {
                var filedata = File.ReadAllText(filename, Encoding.UTF8);
                LayoutSettings layoutSettings = JsonConvert.DeserializeObject<LayoutSettings>(filedata)!;
                return layoutSettings;
            }

            throw new FileNotFoundException($"Could not find layoutsettings file: {filename}");
        }

        /// <inheritdoc />
        public string GetClassRefForLogicDataType(string dataType)
        {
            Application application = GetApplication();
            string classRef = string.Empty;

            DataType? element = application.DataTypes.SingleOrDefault(d => d.Id.Equals(dataType));

            if (element != null)
            {
                classRef = element.AppLogic.ClassRef;
            }

            return classRef;
        }

        /// <inheritdoc />
        public string GetLayouts()
        {
            Dictionary<string, object> layouts = new Dictionary<string, object>();

            // Get FormLayout.json if it exists and return it (for backwards compatibility)
            string fileName = _settings.AppBasePath + _settings.UiFolder + "FormLayout.json";
            if (File.Exists(fileName))
            {
                string fileData = File.ReadAllText(fileName, Encoding.UTF8);
                layouts.Add("FormLayout", JsonConvert.DeserializeObject<object>(fileData)!);
                return JsonConvert.SerializeObject(layouts);
            }

            string layoutsPath = _settings.AppBasePath + _settings.UiFolder + "layouts/";
            if (Directory.Exists(layoutsPath))
            {
                foreach (string file in Directory.GetFiles(layoutsPath))
                {
                    string data = File.ReadAllText(file, Encoding.UTF8);
                    string name = file.Replace(layoutsPath, string.Empty).Replace(".json", string.Empty);
                    layouts.Add(name, JsonConvert.DeserializeObject<object>(data)!);
                }
            }

            return JsonConvert.SerializeObject(layouts);
        }

        /// <inheritdoc />
        public string GetLayoutSets()
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, _settings.LayoutSetsFileName);
            string filedata = null;
            if (File.Exists(filename))
            {
                filedata = File.ReadAllText(filename, Encoding.UTF8);
            }

            return filedata;
        }

        /// <inheritdoc />
        public LayoutSets? GetLayoutSet()
        {
            string? layoutSetsString = GetLayoutSets();
            if (layoutSetsString is not null)
            {
                return System.Text.Json.JsonSerializer.Deserialize<LayoutSets>(layoutSetsString, DESERIALIZER_OPTIONS);
            }

            return null;
        }

        /// <inheritdoc />
        public LayoutSet? GetLayoutSetForTask(string taskId)
        {
            var sets = GetLayoutSet();
            return sets?.Sets?.FirstOrDefault(s => s?.Tasks?.Contains(taskId) ?? false);
        }

        /// <inheritdoc />
        public string GetLayoutsForSet(string layoutSetId)
        {
            Dictionary<string, object> layouts = new Dictionary<string, object>();

            string layoutsPath = _settings.AppBasePath + _settings.UiFolder + layoutSetId + "/layouts/";
            if (Directory.Exists(layoutsPath))
            {
                foreach (string file in Directory.GetFiles(layoutsPath))
                {
                    string data = File.ReadAllText(file, Encoding.UTF8);
                    string name = file.Replace(layoutsPath, string.Empty).Replace(".json", string.Empty);
                    layouts.Add(name, JsonConvert.DeserializeObject<object>(data)!);
                }
            }

            return JsonConvert.SerializeObject(layouts);
        }

        /// <inheritdoc />
        public LayoutModel GetLayoutModel(string? layoutSetId = null)
        {
            string folder = Path.Join(_settings.AppBasePath, _settings.UiFolder, layoutSetId, "layouts");
            var order = GetLayoutSettingsForSet(layoutSetId)?.Pages?.Order;
            if (order is null)
            {
                throw new InvalidDataException("No $Pages.Order field found" + (layoutSetId is null ? "" : $" for layoutSet {layoutSetId}"));
            }

            var layoutModel = new LayoutModel();
            foreach (var page in order)
            {
                var pageBytes = File.ReadAllBytes(Path.Join(folder, page + ".json"));
                // Set the PageName using AsyncLocal before deserializing.
                PageComponentConverter.SetAsyncLocalPageName(page);
                layoutModel.Pages[page] = System.Text.Json.JsonSerializer.Deserialize<PageComponent>(pageBytes.RemoveBom(), DESERIALIZER_OPTIONS) ?? throw new InvalidDataException(page + ".json is \"null\"");
            }

            return layoutModel;
        }

        /// <inheritdoc />
        public string? GetLayoutSettingsStringForSet(string layoutSetId)
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, layoutSetId, _settings.FormLayoutSettingsFileName);
            string? filedata = null;
            if (File.Exists(filename))
            {
                filedata = File.ReadAllText(filename, Encoding.UTF8);
            }

            return filedata;
        }

        /// <inheritdoc />
        public LayoutSettings? GetLayoutSettingsForSet(string? layoutSetId)
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, layoutSetId, _settings.FormLayoutSettingsFileName);
            if (File.Exists(filename))
            {
                string? filedata = null;
                filedata = File.ReadAllText(filename, Encoding.UTF8);
                LayoutSettings? layoutSettings = JsonConvert.DeserializeObject<LayoutSettings>(filedata);
                return layoutSettings;
            }

            return null;
        }

        /// <inheritdoc />
        public byte[] GetRuleConfigurationForSet(string id)
        {
            string legalPath = Path.Join(_settings.AppBasePath, _settings.UiFolder);
            string filename = Path.Join(legalPath, id, _settings.RuleConfigurationJSONFileName);

            PathHelper.EnsureLegalPath(legalPath, filename);

            return ReadFileByte(filename);
        }

        /// <inheritdoc />
        public byte[] GetRuleHandlerForSet(string id)
        {
            string legalPath = Path.Join(_settings.AppBasePath, _settings.UiFolder);
            string filename = Path.Join(legalPath, id, _settings.RuleHandlerFileName);

            PathHelper.EnsureLegalPath(legalPath, filename);

            return ReadFileByte(filename);
        }

        private byte[] ReadFileByte(string fileName)
        {
            byte[] filedata = null;
            if (File.Exists(fileName))
            {
                filedata = File.ReadAllBytes(fileName);
            }

            return filedata;
        }

        private byte[] ReadFileContentsFromLegalPath(string legalPath, string filePath)
        {
            var fullFileName = legalPath + filePath;
            if (!PathHelper.ValidateLegalFilePath(legalPath, fullFileName))
            {
                throw new ArgumentException("Invalid argument", nameof(filePath));
            }

            if (File.Exists(fullFileName))
            {
                return File.ReadAllBytes(fullFileName);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<string?> GetFooter()
        {
            string filename = Path.Join(_settings.AppBasePath, _settings.UiFolder, _settings.FooterFileName);
            string? filedata = null;
            if (File.Exists(filename))
            {
                filedata = await File.ReadAllTextAsync(filename, Encoding.UTF8);
            }

            return filedata;
        }
    }
}
