﻿using System;
using System.Linq;
using E;
using EBot.Models;
using Hime.Redist;

namespace EBot.Helpers
{
    public static class EMessageTimeHelper
    {
        public static EStatus Soon => InMinutes(10);
        public static EStatus Soonish => InMinutes(30);
        
        public static EStatus InMinutes(int minutes) => EStatus.FromState(EState.AvailableLater, DateTimeOffset.Now + TimeSpan.FromMinutes(minutes));
        public static EStatus InHours(int hours) => EStatus.FromState(EState.AvailableLater, DateTimeOffset.Now + TimeSpan.FromHours(hours));

        public static EStatus Read(ASTNode node)
        {
            return node.Symbol.ID switch
            {
                EParser.ID.VariableTime => ReadTime(node),
                EParser.ID.VariableIn => ReadIn(node),
                EParser.ID.VariableNow => EStatus.FromState(EState.Available),
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadIn(ASTNode node)
        {
            ASTNode child = node.Children.First();

            return child.Symbol.ID switch
            {
                EParser.ID.VariableN => ReadN(child),
                EParser.ID.VariableA => ReadA(child),
                ELexer.ID.TerminalInsoon => Soon,
                ELexer.ID.TerminalInsoonish => Soonish,
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadA(ASTNode node)
        {
            return node.Children[1].Symbol.ID switch
            {
                ELexer.ID.TerminalAnhour => InHours(1),
                ELexer.ID.TerminalAminute => InMinutes(1),
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadN(ASTNode node)
        {
            ASTNode number = node.Children[0];

            int time = int.Parse(number.Value);
            
            return node.Children[1].Symbol.ID switch
            {
                EParser.ID.VariableNhours => InHours(time),
                EParser.ID.VariableNminutes => InMinutes(time),
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadTime(ASTNode node)
        {
            ASTFamily children = node.Children;

            ASTNode time = children.First();

            TimeSpan ampm = DateTime.Now.Hour < 12 ? new TimeSpan(0, 0, 0) : new TimeSpan(12, 0, 0);

            if (time.Symbol.ID == ELexer.ID.TerminalTexttime)
            {
                return EStatus.FromState(EState.AvailableLater, DateTime.Today + ampm + TexttimeOffset(time.Value));
            }

            int minute = 0;

            if (children.Count == 2) minute = int.Parse(children.ElementAt(1).Value);

            int hour = int.Parse(time.Value);

            return EStatus.FromState(EState.AvailableLater, DateTime.Today + ampm + new TimeSpan(hour, minute, 0));
        }

        public static TimeSpan TexttimeOffset(string texttime)
        {
            return texttime switch
            {
                "one" => TimeSpan.FromHours(1),
                "two" => TimeSpan.FromHours(2),
                "three" => TimeSpan.FromHours(3),
                "four" => TimeSpan.FromHours(4),
                "five" => TimeSpan.FromHours(5),
                "six" => TimeSpan.FromHours(6),
                "seven" => TimeSpan.FromHours(7),
                "eight" => TimeSpan.FromHours(8),
                "nine" => TimeSpan.FromHours(9),
                "ten" => TimeSpan.FromHours(10),
                "eleven" => TimeSpan.FromHours(11),
                "twelve" => TimeSpan.FromHours(12),
                "noon" => TimeSpan.FromHours(12),
                "midnight" => TimeSpan.FromHours(12),
                _ => TimeSpan.FromHours(-1)
            };
        }
    }
}