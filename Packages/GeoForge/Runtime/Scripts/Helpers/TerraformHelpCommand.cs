// Copyright (c) Spellbound Studios. All rights reserved.

using System.Text;
using Spellbound.Core.Console;

namespace Spellbound.GeoForge {
    [ConsoleCommandClass("terraform", "tf")]
    public class TerraformHelpCommand : ICommand {
        public string Name => "terraform";
        public string Description => "List all terraform commands";
        public string Usage => "terraform";

        /// <summary>
        ///     This is implemented by the ICommand interface and allows us to implement our own logic for this class.
        /// </summary>
        public CommandResult Execute(string[] args) {
            var commands =
                AttributeCommandRegistry.GetUtilityCommandsByClass(typeof(GeoForgeCommands));

            var sb = new StringBuilder();

            sb.AppendLine("=== Terraform Commands ===");
            sb.AppendLine();

            foreach (var (commandName, description) in commands)
                sb.AppendLine($"{commandName,-25} {description}");

            return CommandResult.Ok(sb.ToString());
        }
    }
}