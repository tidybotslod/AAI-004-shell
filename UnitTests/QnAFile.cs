using System;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;

namespace AAI
{
    public class QnAFile
    {
        /// <summary>
        /// Load answer and questions from a CSV formated file. The file expected to have 
        /// "Answer", "question 1", "question 2", ...
        /// 
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        static public List<QnADTO> LoadCSV(string file)
        {
            // Expected format
            // <optional id> <answer> <question> <question> ...
            List<QnADTO> answers = new List<QnADTO>();

            using (TextFieldParser parser = new TextFieldParser(file))
            {
                string[] row;                // 
                                             // Separate rows based on comma.
                parser.Delimiters = new string[] { "," };
                parser.TrimWhiteSpace = true;
                //
                // Ensure the format is correct
                if (parser.EndOfData)
                {
                    // No data
                    throw new Exception("Question and Answer file is empty.");
                }

                while (parser.EndOfData == false)
                {
                    try
                    {
                        row = parser.ReadFields();
                        if (row.Length < 2)
                        {
                            throw new ArgumentException("Missing column.");
                        }
                        //
                        // New Answer, get the key 
                        QnADTO entry = new QnADTO();
                        int index = 0;
                        entry.Answer = row[index++];
                        entry.Questions = new List<String>();
                        for (; index < row.Length; index++)
                        {
                            entry.Questions.Add(row[index]);
                        }
                        answers.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Skipping line ${parser.LineNumber}, ${ex}");
                    }
                }
            }
            return answers;
        }
    }
}