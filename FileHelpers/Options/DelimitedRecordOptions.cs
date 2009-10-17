#region "  � Copyright 2005-07 to Marcos Meli - http://www.devoo.net"

// Errors, suggestions, contributions, send a mail to: marcos@filehelpers.com.

#endregion

using System;

namespace FileHelpers
{
	/// <summary>
	/// This class allows you to set some options of the delimited records but at runtime.
	/// With this options the library is more flexible than never.
	/// </summary>
	public sealed class DelimitedRecordOptions: RecordOptions
	{
		
		internal DelimitedRecordOptions(IRecordInfo info)
			:base(info)
		{}
		 
		/// <summary>
		/// The delimiter used to identify each field in the data.
		/// </summary>
		public string Delimiter
		{
			get
			{
				return ((DelimitedField) mRecordInfo.Fields[0]).Separator;
			}
			set
			{
				for(int i = 0; i < mRecordInfo.FieldCount ;i++)
					((DelimitedField) mRecordInfo.Fields[i]).Separator = value;
			}

		}

	}
}
