#region "  � Copyright 2005-07 to Marcos Meli - http://www.marcosmeli.com.ar" 

// Errors, suggestions, contributions, send a mail to: marcos@filehelpers.com.

#endregion

using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace FileHelpers
{

	/// <summary>Base class for all Field Types. Implements all the basic functionality of a field in a typed file.</summary>
	internal abstract class FieldBase
	{

        public string FielName
        {
            get { return mFieldInfo.Name; }
        }

        public Type FielType
        {
            get { return mFieldType; }
        }

		#region "  Private & Internal Fields  "

		private static Type strType = typeof (string);

		internal Type mFieldType;
		internal bool mIsStringField;
		internal FieldInfo mFieldInfo;

		internal TrimMode mTrimMode = TrimMode.None;
		internal Char[] mTrimChars = null;
		internal bool mIsOptional = false;
		internal bool mNextIsOptional = false;
		internal bool mInNewLine = false;

		internal bool mIsFirst = false;
		internal bool mIsLast = false;
		internal bool mTrailingArray = false;

		internal bool mIsArray = false;
		internal Type mArrayType;
		internal int mArrayMinLength;
		internal int mArrayMaxLength;

		internal object mNullValue = null;
		//internal bool mNullValueOnWrite = false;

#if NET_2_0
		private bool mIsNullableType = false;
#endif
		#endregion

		#region "  Constructor  " 

		internal FieldBase(FieldInfo fi)
		{
			mFieldInfo = fi;
			mFieldType = mFieldInfo.FieldType;
			mIsStringField = mFieldType == strType;

			object[] attribs = fi.GetCustomAttributes(typeof (FieldConverterAttribute), true);

			if (attribs.Length > 0)
            {
                FieldConverterAttribute conv = (FieldConverterAttribute)attribs[0];
                mConvertProvider = conv.Converter;
                conv.ValidateTypes(mFieldInfo);
            }
			else
				mConvertProvider = ConvertHelpers.GetDefaultConverter(fi.Name, mFieldType);

			if (mConvertProvider != null)
				mConvertProvider.mDestinationType = fi.FieldType;

			attribs = fi.GetCustomAttributes(typeof (FieldNullValueAttribute), true);

			if (attribs.Length > 0)
			{
				mNullValue = ((FieldNullValueAttribute) attribs[0]).NullValue;
//				mNullValueOnWrite = ((FieldNullValueAttribute) attribs[0]).NullValueOnWrite;

				if (mNullValue != null)
				{
					if (! mFieldType.IsAssignableFrom(mNullValue.GetType()))
						throw new BadUsageException("The NullValue is of type: " + mNullValue.GetType().Name +
						                            " that is not asignable to the field " + mFieldInfo.Name + " of type: " +
						                            mFieldType.Name);
				}
			}


#if NET_2_0
			mIsNullableType = mFieldType.IsValueType &&
									mFieldType.IsGenericType && 
									mFieldType.GetGenericTypeDefinition() == typeof(Nullable<>);
#endif
		}

		#endregion


		private static char[] WhitespaceChars = new char[] 
			 { 
				 '\t', '\n', '\v', '\f', '\r', ' ', '\x00a0', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', 
				 '\u2009', '\u200a', '\u200b', '\u3000', '\ufeff'
			 };


		#region "  MustOverride (String Handling)  " 

		internal abstract ExtractedInfo ExtractFieldString(LineInfo line);

        internal abstract void CreateFieldString(StringBuilder sb, object fieldValue);

        internal string CreateFieldString(object fieldValue)
		{
			if (mConvertProvider == null)
			{
				if (fieldValue == null)
					return string.Empty;
				else
					return fieldValue.ToString();

//				else if (mNullValueOnWrite && fieldValue.Equals(mNullValue))
//					res = string.Empty;

			}
			else
			{
				return mConvertProvider.FieldToString(fieldValue);
			}
		}

		internal int mCharsToDiscard = 0;

		#endregion

		internal ConverterBase mConvertProvider;

		#region "  ExtractValue  " 

// object[] values, int index, ForwardReader reader
		internal object ExtractFieldValue(LineInfo line)
		{
			//-> extract only what I need

			if (this.mInNewLine == true)
			{
				if (line.EmptyFromPos() == false)
					throw new BadUsageException(line, "Text '" + line.CurrentString +
					                            "' found before the new line of the field: " + mFieldInfo.Name +
					                            " (this is not allowed when you use [FieldInNewLine])");

				line.ReLoad(line.mReader.ReadNextLine());

				if (line.mLineStr == null)
					throw new BadUsageException(line, "End of stream found parsing the field " + mFieldInfo.Name +
					                            ". Please check the class record.");
			}

			if (mIsArray == false)
			{
				ExtractedInfo info = ExtractFieldString(line);
				if (info.mCustomExtractedString == null)
					line.mCurrentPos = info.ExtractedTo + 1;

				line.mCurrentPos += mCharsToDiscard; //total;

				return AssignFromString(info, line);
			}
			else
			{
				if (mArrayMinLength <= 0)
					mArrayMinLength = 0;

				int i = 0;

				ArrayList res = new ArrayList(Math.Max(mArrayMinLength, 10));

				while (line.IsEOL() == false && i < mArrayMaxLength)
				{
                    //mIgnoreExtraLength = i < (mArrayMaxLength - 1);
					ExtractedInfo info = ExtractFieldString(line);
					if (info.mCustomExtractedString == null)
						line.mCurrentPos = info.ExtractedTo + 1;

					line.mCurrentPos += mCharsToDiscard; //total;

					res.Add(AssignFromString(info, line));
                    i++;
				}

                if (res.Count < mArrayMinLength)
                    throw new InvalidOperationException("Line: " + line.mReader.LineNumber.ToString() + " Column: " + line.mCurrentPos.ToString() + " Field: " + mFieldInfo.Name + ". The array has only "+ res.Count.ToString() +" values, less than the minimum length of " + mArrayMinLength.ToString());

                else if (mIsLast && line.IsEOL() == false)
                    throw new InvalidOperationException("Line: " + line.mReader.LineNumber.ToString() + " Column: " + line.mCurrentPos.ToString() + " Field: " + mFieldInfo.Name + ". The array has more values than the maximum length of " + mArrayMaxLength.ToString());

				return res.ToArray(mArrayType);
		
			}


			//-> discard the part that I use


			//TODO: Uncoment this for Quoted Handling
//			if (info.NewRestOfLine != null)
//			{
//				if (info.NewRestOfLine.Length < CharsToDiscard())
//					return info.NewRestOfLine;
//				else
//					return info.NewRestOfLine.Substring(CharsToDiscard());
//			}
//			else
//			{
//				int total;
//				if (info.CharsRemoved >= line.mLine.Length)
//					total = line.mLine.Length;
//				else
//					total = info.CharsRemoved + CharsToDiscard();

				//return buffer.Substring(total);
//			}


		}

		#region "  AssignFromString  " 

        internal object AssignFromString(ExtractedInfo fieldString, LineInfo line)
        {
            object val;

            switch (mTrimMode)
            {
                case TrimMode.None:
                    break;

                case TrimMode.Both:
                    fieldString.TrimBoth(mTrimChars);
                    break;

                case TrimMode.Left:
                    fieldString.TrimStart(mTrimChars);
                    break;

                case TrimMode.Right:
                    fieldString.TrimEnd(mTrimChars);
                    break;
            }

            try
            {

                if (mConvertProvider == null)
                {
                    if (mIsStringField)
                        val = fieldString.ExtractedString();
                    else
                    {
                        // Trim it to use Convert.ChangeType
                        fieldString.TrimBoth(WhitespaceChars);

                        if (fieldString.Length == 0)
                        {
                            // Empty stand for null
                            val = GetNullValue(line);
                        }
                        else
                        {
                            val = Convert.ChangeType(fieldString.ExtractedString(), mFieldType, null);
                        }
                    }
                }
                else
                {
                    if (mConvertProvider.CustomNullHandling == false &&
                        fieldString.HasOnlyThisChars(WhitespaceChars))
                    {
                        val = GetNullValue(line);
                    }
                    else
                    {
                        string from = fieldString.ExtractedString();
                        val = mConvertProvider.StringToField(from);

                        if (val == null)
                            val = GetNullValue(line);

                    }
                }

                return val;
            }
            catch (ConvertException ex)
            {
                throw ConvertException.ReThrowException(ex, mFieldInfo.Name, line.mReader.LineNumber, fieldString.ExtractedFrom + 1);
            }
			catch (BadUsageException)
			{
				throw;
			}
            catch (Exception ex)
            {
                if (mConvertProvider == null || mConvertProvider.GetType().Assembly == typeof(FieldBase).Assembly)
                    throw new ConvertException(fieldString.ExtractedString(), mFieldType, mFieldInfo.Name, line.mReader.LineNumber, fieldString.ExtractedFrom + 1, ex.Message, ex);
                else
                    throw new ConvertException(fieldString.ExtractedString(), mFieldType, mFieldInfo.Name, line.mReader.LineNumber, fieldString.ExtractedFrom + 1, "Your custom converter: " + mConvertProvider.GetType().Name + " throws an " + ex.GetType().Name +" with the message: " + ex.Message, ex);
            }
        }

		private object GetNullValue(LineInfo line)
		{
			if (mNullValue == null)
			{
				if (mFieldType.IsValueType)
				{

#if NET_2_0
				   if ( mIsNullableType )
						return null;
#endif

				string msg = "Empty value found for the Field: '" + mFieldInfo.Name + "' Class: '" + mFieldInfo.DeclaringType.Name + "'. ";

#if NET_2_0	
					throw new BadUsageException(line, msg + "You must use the FieldNullValue attribute because this is a ValueType and can�t be null or you can use the Nullable Types feature of the .NET framework.");
#else
					throw new BadUsageException(line, msg + "You must use the FieldNullValue attribute because this is a ValueType and can�t be null.");
#endif

				}
				else
					return null;
			}
			else
				return mNullValue;
		}

		#endregion

		#region "  CreateValueForField  " 

		public object CreateValueForField(object fieldValue)
		{
			object val = null;

			if (fieldValue == null)
			{
				if (mNullValue == null)
				{
					if (mFieldType.IsValueType)
						throw new BadUsageException("Null Value found. You must specify a NullValueAttribute in the " + mFieldInfo.Name +
						                            " field of type " + mFieldInfo.FieldType.Name + ", because this is a ValueType.");
					else
						val = null;
				}
				else
				{
					val = mNullValue;
				}
			}
			else if (mFieldType == fieldValue)
				val = fieldValue;
			else
			{
				if (mConvertProvider == null)
					val = Convert.ChangeType(fieldValue, mFieldType, null);
				else
				{
					try
					{
						val = Convert.ChangeType(fieldValue, mFieldType, null);
					}
					catch
					{
						val = mConvertProvider.StringToField(fieldValue.ToString());
					}
				}
			}

			return val;
		}

		#endregion


		#endregion

		#region "  AssignToString  " 

		internal void AssignToString(StringBuilder sb, object fieldValue)
		{
			if (this.mInNewLine == true)
				sb.Append(StringHelper.NewLine);

            if (mIsArray)
            {
                if (fieldValue == null)
                    return;

                foreach (object val in (Array) fieldValue)
                {
                    CreateFieldString(sb, val);
                }
            }
            else
                CreateFieldString(sb, fieldValue);
		}

		#endregion
	}
}
