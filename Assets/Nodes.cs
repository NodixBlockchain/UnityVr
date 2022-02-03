using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//using FASTER.core;

public class Nodes : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        /*
        var log = Devices.CreateLogDevice("hlog.log"); // backing storage device

        // hash table size (number of 64-byte buckets)
        // log settings (devices, page size, memory size, etc.)
        var store = new FasterKV<long, long>(1L << 20, new LogSettings { LogDevice = log });

        // Create a session per sequence of interactions with FASTER
        // We use default callback functions with a custom merger: RMW merges input by adding it to value
        var s = store.NewSession(new SimpleFunctions<long, long>((a, b) => a + b));
        long key = 1, value = 1, input = 10, output = 0;
        /*
        // Upsert and Read
        s.Upsert(ref key, ref value);
        s.Read(ref key, ref output);
        Debug.Assert(output == value);

        // Read-Modify-Write (add input to value)
        s.RMW(ref key, ref input);
        s.RMW(ref key, ref input);
        s.Read(ref key, ref output);
        Debug.Assert(output == value + 20);
        */
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
