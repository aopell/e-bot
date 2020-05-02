/*
 * WARNING: this file has been generated by
 * Hime Parser Generator 3.4.0.0
 */
using System.Collections.Generic;
using System.IO;
using Hime.Redist;
using Hime.Redist.Lexer;

namespace E
{
	/// <summary>
	/// Represents a lexer
	/// </summary>
	internal class ELexer : ContextSensitiveLexer
	{
		/// <summary>
		/// The automaton for this lexer
		/// </summary>
		private static readonly Automaton commonAutomaton = Automaton.Find(typeof(ELexer), "ELexer.bin");
		/// <summary>
		/// Contains the constant IDs for the terminals for this lexer
		/// </summary>
		public class ID
		{
			/// <summary>
			/// The unique identifier for terminal WHITE_SPACE
			/// </summary>
			public const int TerminalWhiteSpace = 0x0004;
			/// <summary>
			/// The unique identifier for terminal SEPARATOR
			/// </summary>
			public const int TerminalSeparator = 0x0005;
			/// <summary>
			/// The unique identifier for terminal YFINAL
			/// </summary>
			public const int TerminalYfinal = 0x0006;
			/// <summary>
			/// The unique identifier for terminal WITHIN
			/// </summary>
			public const int TerminalWithin = 0x0007;
			/// <summary>
			/// The unique identifier for terminal LIKE
			/// </summary>
			public const int TerminalLike = 0x0008;
			/// <summary>
			/// The unique identifier for terminal TONIGHT
			/// </summary>
			public const int TerminalTonight = 0x0009;
			/// <summary>
			/// The unique identifier for terminal MINS
			/// </summary>
			public const int TerminalMins = 0x000A;
			/// <summary>
			/// The unique identifier for terminal INSOON
			/// </summary>
			public const int TerminalInsoon = 0x000B;
			/// <summary>
			/// The unique identifier for terminal INSOONISH
			/// </summary>
			public const int TerminalInsoonish = 0x000C;
			/// <summary>
			/// The unique identifier for terminal SOON
			/// </summary>
			public const int TerminalSoon = 0x000D;
			/// <summary>
			/// The unique identifier for terminal SOONISH
			/// </summary>
			public const int TerminalSoonish = 0x000E;
			/// <summary>
			/// The unique identifier for terminal AT_OR_BEFORE
			/// </summary>
			public const int TerminalAtOrBefore = 0x000F;
			/// <summary>
			/// The unique identifier for terminal AMINUTE
			/// </summary>
			public const int TerminalAminute = 0x0010;
			/// <summary>
			/// The unique identifier for terminal ANHOUR
			/// </summary>
			public const int TerminalAnhour = 0x0011;
			/// <summary>
			/// The unique identifier for terminal RANGE
			/// </summary>
			public const int TerminalRange = 0x0012;
			/// <summary>
			/// The unique identifier for terminal INTEGER
			/// </summary>
			public const int TerminalInteger = 0x0013;
			/// <summary>
			/// The unique identifier for terminal NUMBER
			/// </summary>
			public const int TerminalNumber = 0x0014;
			/// <summary>
			/// The unique identifier for terminal MINUTE
			/// </summary>
			public const int TerminalMinute = 0x0015;
			/// <summary>
			/// The unique identifier for terminal TEXTTIME
			/// </summary>
			public const int TerminalTexttime = 0x0016;
			/// <summary>
			/// The unique identifier for terminal HOUR
			/// </summary>
			public const int TerminalHour = 0x0017;
			/// <summary>
			/// The unique identifier for terminal E
			/// </summary>
			public const int TerminalE = 0x0018;
		}
		/// <summary>
		/// Contains the constant IDs for the contexts for this lexer
		/// </summary>
		public class Context
		{
			/// <summary>
			/// The unique identifier for the default context
			/// </summary>
			public const int Default = 0;
			/// <summary>
			/// The unique identifier for context in
			/// </summary>
			public const int In = 0x0001;
			/// <summary>
			/// The unique identifier for context like
			/// </summary>
			public const int Like = 0x0002;
			/// <summary>
			/// The unique identifier for context min
			/// </summary>
			public const int Min = 0x0003;
			/// <summary>
			/// The unique identifier for context soon
			/// </summary>
			public const int Soon = 0x0004;
			/// <summary>
			/// The unique identifier for context at
			/// </summary>
			public const int At = 0x0005;
			/// <summary>
			/// The unique identifier for context one
			/// </summary>
			public const int One = 0x0006;
			/// <summary>
			/// The unique identifier for context range
			/// </summary>
			public const int Range = 0x0007;
			/// <summary>
			/// The unique identifier for context time
			/// </summary>
			public const int Time = 0x0008;
			/// <summary>
			/// The unique identifier for context start
			/// </summary>
			public const int Start = 0x0009;
		}
		/// <summary>
		/// The collection of terminals matched by this lexer
		/// </summary>
		/// <remarks>
		/// The terminals are in an order consistent with the automaton,
		/// so that terminal indices in the automaton can be used to retrieve the terminals in this table
		/// </remarks>
		private static readonly Symbol[] terminals = {
			new Symbol(0x0001, "ε"),
			new Symbol(0x0002, "$"),
			new Symbol(0x0004, "WHITE_SPACE"),
			new Symbol(0x0005, "SEPARATOR"),
			new Symbol(0x0006, "YFINAL"),
			new Symbol(0x0007, "WITHIN"),
			new Symbol(0x0008, "LIKE"),
			new Symbol(0x0009, "TONIGHT"),
			new Symbol(0x000A, "MINS"),
			new Symbol(0x000B, "INSOON"),
			new Symbol(0x000C, "INSOONISH"),
			new Symbol(0x000D, "SOON"),
			new Symbol(0x000E, "SOONISH"),
			new Symbol(0x000F, "AT_OR_BEFORE"),
			new Symbol(0x0010, "AMINUTE"),
			new Symbol(0x0011, "ANHOUR"),
			new Symbol(0x0012, "RANGE"),
			new Symbol(0x0013, "INTEGER"),
			new Symbol(0x0014, "NUMBER"),
			new Symbol(0x0015, "MINUTE"),
			new Symbol(0x0016, "TEXTTIME"),
			new Symbol(0x0017, "HOUR"),
			new Symbol(0x0018, "E"),
			new Symbol(0x0026, "now"),
			new Symbol(0x0028, "hr"),
			new Symbol(0x0029, " hour"),
			new Symbol(0x002A, "s"),
			new Symbol(0x0032, ":"),
			new Symbol(0x0038, " "),
			new Symbol(0x0039, "?") };
		/// <summary>
		/// Initializes a new instance of the lexer
		/// </summary>
		/// <param name="input">The lexer's input</param>
		public ELexer(string input) : base(commonAutomaton, terminals, 0x0005, input) {}
		/// <summary>
		/// Initializes a new instance of the lexer
		/// </summary>
		/// <param name="input">The lexer's input</param>
		public ELexer(TextReader input) : base(commonAutomaton, terminals, 0x0005, input) {}
	}
}