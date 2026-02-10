using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class WebGetStudentCSVTest : MonoBehaviour
{
    private async void Start()
    {
        string result = await GetWebText("https://raw.githubusercontent.com/mongilteacher/skku2_script_study/refs/heads/main/students.csv");

        List<Person> people = new List<Person>();

        // 1. 읽어온 CSV 파일을 파싱
        string[] lines = result.Split('\n');

        // 첫 번째 줄은 헤더이므로 스킵 (i = 1부터 시작)
        for (int i = 1; i < lines.Length; i++)
        {
            // 빈 줄 체크
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            // 쉼표로 분리
            string[] values = lines[i].Split(',');

            // 2. Person 도메인 클래스에 담기
            if (values.Length >= 2)
            {
                Person person = new Person();

                if (int.TryParse(values[0].Trim(), out int num))
                {
                    person.Number = num;
                }

                person.Name = values[1].Trim();

                if (int.TryParse(values[2].Trim(), out int age))
                {
                    person.Age = age;
                }

                people.Add(person);
            }
        }

        // 3. List<Person> people 도메인 클래스들을 순회하며 출력
        foreach (Person person in people)
        {
            Debug.Log($"{person.Number}, {person.Name}, {person.Age}");
        }
    }

    private async UniTask<string> GetWebText(string url)
    {
        var txt = (await UnityWebRequest.Get(url).SendWebRequest()).downloadHandler.text;
        return txt;
    }
}

public class Person
{
    public int Number;
    public string Name;
    public int Age;
}
