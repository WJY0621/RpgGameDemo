using System.Collections.Generic;

public class DLNodeErrorData
{
    public DLErrorData ErrorData { get; set; }
    public List<DLNode> Nodes { get; set; }

    public DLNodeErrorData()
    {
        ErrorData = new DLErrorData();
        Nodes = new List<DLNode>();
    }
}
