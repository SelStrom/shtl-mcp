using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// F3/AC3.6–3.8: обнаружение и регистрация кастомных инструментов host-проекта — классов `ITool`,
    /// помеченных `[McpTool]`. Без форка shtl-mcp и без ручной регистрации. Изоляция ошибок (битый тул не
    /// роняет старт) + приоритет встроенных при коллизии имён (они регистрируются первыми).
    public static class ToolDiscovery
    {
        /// Live-путь: найти все `[McpTool]`-классы в загруженных Editor-сборках (Unity `TypeCache`, дёшево) и
        /// зарегистрировать. Зовётся из `ShtlMcpServer.EnsureStarted` ПОСЛЕ встроенных и на каждом domain reload.
        public static void DiscoverAndRegister(ToolRegistry registry)
        {
            // Порядок TypeCache не определён Unity → сортируем по FullName для ДЕТЕРМИНИРОВАННОГО выбора при
            // коллизии имён кастом-vs-кастом (AC3.7): лексикографически первый FullName побеждает.
            RegisterFrom(
                TypeCache.GetTypesWithAttribute<McpToolAttribute>().OrderBy(t => t.FullName, StringComparer.Ordinal),
                registry);
        }

        /// Тестируемое ядро: зарегистрировать инструменты из ЯВНОГО списка типов-кандидатов. Пропускает (через
        /// onSkip) типы, не реализующие `ITool` / абстрактные / без public parameterless-ctor / чей КОНТРАКТ
        /// (`Name`/`Description`/`InputSchema`-геттер) бросает / с пустым `Name` / с именем уже зарегистрированного.
        /// `onSkip == null` → предупреждение в консоль Unity. Изоляция per-tool: битый кандидат пропускается,
        /// остальные регистрируются (INV-5-стиль изоляции при обнаружении).
        public static void RegisterFrom(IEnumerable<Type> candidates, ToolRegistry registry,
            Action<Type, string> onSkip = null)
        {
            onSkip = onSkip ?? DefaultWarn;
            foreach (var t in candidates)
            {
                if (t == null)
                {
                    continue;
                }
                if (t.IsAbstract || t.IsInterface || !typeof(ITool).IsAssignableFrom(t))
                {
                    onSkip(t, "класс не реализует ITool (или абстрактный)");
                    continue;
                }

                try
                {
                    var tool = (ITool)Activator.CreateInstance(t);
                    var name = tool.Name;   // геттеры автора могут бросить — прогреваем ВЕСЬ контракт здесь,
                    _ = tool.Description;    // чтобы битый кастомный тул был пропущен на обнаружении и НЕ отравил
                    _ = tool.InputSchema;   // tools/list позже (ToolRegistry.List читает Description/InputSchema).
                    if (string.IsNullOrEmpty(name))
                    {
                        onSkip(t, "пустой Name");
                        continue;
                    }
                    if (registry.Contains(name))
                    {
                        onSkip(t, "имя '" + name + "' уже занято (встроенным или ранее найденным) — пропуск без override");
                        continue;
                    }
                    registry.Register(tool);
                }
                catch (Exception e)
                {
                    onSkip(t, "ctor / контракт (Name/Description/InputSchema) упал: " + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        static void DefaultWarn(Type t, string why)
        {
            Debug.LogWarning("[shtl-mcp] custom tool '" + (t != null ? t.FullName : "?") + "' skipped: " + why);
        }
    }
}
