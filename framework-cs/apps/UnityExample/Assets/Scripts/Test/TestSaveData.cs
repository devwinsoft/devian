using UnityEngine;
using System.Collections.Generic;

public class TestSaveData
{
    public class TestItemData
    {
        public string item_id;
        public int count;
    }
    
    public string name;
    public List<TestItemData> items = new List<TestItemData>();
}
