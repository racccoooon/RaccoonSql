namespace RaccoonSql.CoreRework.Internal.Utils;

public static class ListExtensions
{
    public static List<List<T>> CrossProduct<T>(this IEnumerable<IReadOnlyList<T>> terms)
    {
        List<List<T>> crossProduct = [[]];
        foreach (var list in terms)
        {
            switch (list.Count)
            {
                case 0:
                    return []; // if any of the input lists has no elements then the cross product is empty
                case 1:
                {
                    foreach (var list1 in crossProduct)
                    {
                        list1.Add(list[0]);
                    }

                    break;
                }
                default:
                {
                    List<List<T>> crossProductNew = [];
                    foreach (var list1 in crossProduct)
                    {
                        foreach (var val in list)
                        {
                            crossProductNew.Add([..list1, val]);
                        } 
                    }

                    crossProduct = crossProductNew;
                    break;
                }
            }
        }

        return crossProduct;
    }
}