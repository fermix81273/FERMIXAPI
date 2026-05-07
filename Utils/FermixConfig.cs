using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using FermixAPI.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FermixAPI.Utils
{
    /// <summary>
    /// Утилиты для работы с конфигурациями
    /// </summary>
    public static class FermixConfigUtils
    {
        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        
        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        
        /// <summary>
        /// Путь к директории конфигураций
        /// </summary>
        public static string ConfigDirectory => Path.Combine(Paths.Configs, "FermixAPI");
        
        /// <summary>
        /// Инициализация директории конфигураций
        /// </summary>
        public static void Initialize()
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
        }
        
        /// <summary>
        /// Загрузить конфигурацию из файла
        /// </summary>
        public static T Load<T>(string fileName) where T : class, new()
        {
            var path = GetConfigPath(fileName);
            
            if (!File.Exists(path))
            {
                var config = new T();
                Save(fileName, config);
                return config;
            }
            
            try
            {
                var yaml = File.ReadAllText(path);
                return Deserializer.Deserialize<T>(yaml) ?? new T();
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Ошибка загрузки конфигурации {fileName}: {ex.Message}");
                return new T();
            }
        }
        
        /// <summary>
        /// Сохранить конфигурацию в файл
        /// </summary>
        public static void Save<T>(string fileName, T config) where T : class
        {
            Initialize();
            var path = GetConfigPath(fileName);
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                Directory.CreateDirectory(parent);
            
            try
            {
                var yaml = Serializer.Serialize(config);
                File.WriteAllText(path, yaml);
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Ошибка сохранения конфигурации {fileName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Перезагрузить конфигурацию
        /// </summary>
        public static T Reload<T>(string fileName) where T : class, new()
            => Load<T>(fileName);
        
        /// <summary>
        /// Удалить конфигурацию
        /// </summary>
        public static bool Delete(string fileName)
        {
            var path = GetConfigPath(fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Проверить существование конфигурации
        /// </summary>
        public static bool Exists(string fileName)
            => File.Exists(GetConfigPath(fileName));
        
        /// <summary>
        /// Получить путь к конфигурации
        /// </summary>
        public static string GetConfigPath(string fileName)
        {
            if (!fileName.EndsWith(".yml") && !fileName.EndsWith(".yaml"))
                fileName += ".yml";
            return Path.Combine(ConfigDirectory, fileName);
        }
        
        /// <summary>
        /// Получить все конфигурации в директории
        /// </summary>
        public static IEnumerable<string> GetAllConfigs()
        {
            Initialize();
            return Directory.GetFiles(ConfigDirectory, "*.yml")
                .Concat(Directory.GetFiles(ConfigDirectory, "*.yaml"))
                .Select(Path.GetFileNameWithoutExtension);
        }
    }
    
    /// <summary>
    /// Базовый класс для конфигураций плагинов
    /// </summary>
    public abstract class FermixPluginConfig
    {
        /// <summary>
        /// Включен ли плагин
        /// </summary>
        public virtual bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Режим отладки
        /// </summary>
        public virtual bool DebugMode { get; set; } = false;
    }
    
    /// <summary>
    /// Кэшируемая конфигурация
    /// </summary>
    public class CachedConfig<T> where T : class, new()
    {
        private T _config;
        private readonly string _fileName;
        private DateTime _lastLoad;
        private readonly TimeSpan _cacheTime;
        
        public CachedConfig(string fileName, TimeSpan? cacheTime = null)
        {
            _fileName = fileName;
            _cacheTime = cacheTime ?? TimeSpan.FromMinutes(5);
        }
        
        /// <summary>
        /// Получить конфигурацию (с кэшированием)
        /// </summary>
        public T Get()
        {
            if (_config == null || DateTime.Now - _lastLoad > _cacheTime)
            {
                _config = FermixConfigUtils.Load<T>(_fileName);
                _lastLoad = DateTime.Now;
            }
            return _config;
        }
        
        /// <summary>
        /// Принудительно перезагрузить конфигурацию
        /// </summary>
        public T Reload()
        {
            _config = FermixConfigUtils.Load<T>(_fileName);
            _lastLoad = DateTime.Now;
            return _config;
        }
        
        /// <summary>
        /// Сохранить конфигурацию
        /// </summary>
        public void Save()
        {
            if (_config != null)
                FermixConfigUtils.Save(_fileName, _config);
        }
        
        /// <summary>
        /// Сбросить кэш
        /// </summary>
        public void Invalidate()
        {
            _config = null;
        }
    }
    
    /// <summary>
    /// Реактивная конфигурация с автосохранением
    /// </summary>
    public class ReactiveConfig<T> where T : class, new()
    {
        private T _config;
        private readonly string _fileName;
        private readonly bool _autoSave;
        
        public event Action<T> OnConfigChanged;
        
        public ReactiveConfig(string fileName, bool autoSave = true)
        {
            _fileName = fileName;
            _autoSave = autoSave;
            Load();
        }
        
        /// <summary>
        /// Текущая конфигурация
        /// </summary>
        public T Value
        {
            get => _config;
            set
            {
                _config = value;
                OnConfigChanged?.Invoke(_config);
                if (_autoSave)
                    Save();
            }
        }
        
        /// <summary>
        /// Загрузить конфигурацию
        /// </summary>
        public void Load()
        {
            _config = FermixConfigUtils.Load<T>(_fileName);
        }
        
        /// <summary>
        /// Сохранить конфигурацию
        /// </summary>
        public void Save()
        {
            FermixConfigUtils.Save(_fileName, _config);
        }
        
        /// <summary>
        /// Изменить конфигурацию
        /// </summary>
        public void Modify(Action<T> modifier)
        {
            modifier(_config);
            OnConfigChanged?.Invoke(_config);
            if (_autoSave)
                Save();
        }
    }
}
