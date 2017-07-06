using System;
using System.Collections.Generic;
using System.Text;
using LogJoint.RegularExpressions;

namespace LogJoint
{
	public static class Search
	{
		public class SearchState
		{
			internal Options options;
			internal IRegex re;
			internal MessageFlag typeMask;
			internal MessageFlag msgTypeMask;
			internal IMatch searchMatch;
		};

		public class TemplateException : Exception
		{
		};

		public struct Options
		{
			public string Template;
			public bool WholeWord;
			public bool Regexp;
			public IFilterScope Scope;
			public bool MatchCase;
			public MessageFlag TypesToLookFor;
			public bool ReverseSearch;

			/// <summary>
			/// Preprocesses the search options and returns an opaque object
			/// that holds the state needed to efficiently search many times using the search options.
			/// Different threads can not share the returned state object. Each thread has to call this method.
			/// </summary>
			public SearchState BeginSearch()
			{
				SearchState ret = new SearchState() { 
					options = this,
					typeMask = MessageFlag.TypeMask & TypesToLookFor,
					msgTypeMask = MessageFlag.ContentTypeMask & TypesToLookFor
				};
				if (!string.IsNullOrEmpty(Template))
				{
					if (Regexp)
					{
						ReOptions reOpts = ReOptions.AllowPatternWhitespaces;
						if (!MatchCase)
							reOpts |= ReOptions.IgnoreCase;
						if (ReverseSearch)
							reOpts |= ReOptions.RightToLeft;
						try
						{
							ret.re = RegexFactory.Instance.Create(Template, reOpts);
						}
						catch (Exception)
						{
							throw new TemplateException();
						}
					}
					else
					{
						if (!MatchCase)
							ret.options.Template = ret.options.Template.ToLower();
					}
				}
				return ret;
			}
		};

		public struct MatchedTextRange
		{
			readonly public int MatchBegin;
			readonly public int MatchEnd;
			readonly public bool WholeTextMatched;
			readonly public StringSlice SourceText;

			public MatchedTextRange(StringSlice sourceText, int b, int e, bool wholeTextMatched)
			{
				this.SourceText = sourceText;
				this.MatchBegin = b;
				this.MatchEnd = e;
				this.WholeTextMatched = wholeTextMatched;
			}
		};

		public static MatchedTextRange? SearchInMessageText(
			IMessage msg, 
			SearchState state, 
			bool searchInRawText,
			int? startTextPosition = null)
		{
			MessageFlag typeMask = state.typeMask;
			MessageFlag msgTypeMask = state.msgTypeMask;

			MessageFlag msgFlags = msg.Flags;
			if (state.options.TypesToLookFor != MessageFlag.None) // None means All
			{
				var msgType = msgFlags & typeMask;
				if (msgType == 0)
					return null;
				if (msgType == MessageFlag.Content && (msgFlags & msgTypeMask) == 0)
					return null;
			}

			if (state.options.Scope?.ContainsMessage(msg) == false)
			{
				return null;
			}

			StringSlice sourceText;
			if (searchInRawText)
			{
				sourceText = msg.RawText;
				if (!sourceText.IsInitialized)
					sourceText = msg.Text;
			}
			else
			{
				sourceText = msg.Text;
			}
			return SearchInText(sourceText, state, startTextPosition);
		}

		public static MatchedTextRange? SearchInText(StringSlice text, SearchState state, int? startTextPosition)
		{
			IRegex re = state.re;

			// matched string position
			int matchBegin = 0; // index of the first matched char
			int matchEnd = 0; // index of following after the last matched one
			bool wholeTextMatched = false;

			if (!string.IsNullOrEmpty(state.options.Template)) // empty/null template means that text matching isn't required, i.e. match any input
			{
				int textPos;

				if (startTextPosition.HasValue)
					textPos = startTextPosition.Value;
				else if (state.options.ReverseSearch)
					textPos = text.Length;
				else
					textPos = 0;

				for (; ; )
				{
					if (re != null)
					{
						if (!re.Match(text, textPos, ref state.searchMatch))
							return null;
						matchBegin = state.searchMatch.Index;
						matchEnd = matchBegin + state.searchMatch.Length;
					}
					else
					{
						StringComparison cmp = state.options.MatchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
						int i;
						if (state.options.ReverseSearch)
							i = text.LastIndexOf(state.options.Template, textPos, cmp);
						else
							i = text.IndexOf(state.options.Template, textPos, cmp);
						if (i < 0)
							return null;
						matchBegin = i;
						matchEnd = matchBegin + state.options.Template.Length;
					}

					if (state.options.WholeWord && !IsWordBoundary(text, matchBegin, matchEnd))
					{
						textPos = state.options.ReverseSearch ? matchBegin : matchEnd;
						continue;
					}

					break;
				}
			}
			else
			{
				matchBegin = 0;
				matchEnd = text.Length;
				wholeTextMatched = true;
			}

			return new MatchedTextRange(text, matchBegin, matchEnd, wholeTextMatched);
		}

		public static bool IsWordBoundary(this StringSlice stringSlice, int substringBegin, int substringEnd)
		{
			if (substringBegin > 0)
				if (StringUtils.IsWordChar(stringSlice[substringBegin - 1]))
					return false;
			if (substringEnd <= stringSlice.Length - 1)
				if (StringUtils.IsWordChar(stringSlice[substringEnd]))
					return false;
			return true;
		}
	};
}
