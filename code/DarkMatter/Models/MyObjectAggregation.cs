namespace DarkMatter.Models;

public record MyObjectAggregation : MyObject
{
	#region Price Aggregation Properties

	public double Price_Sum { get; set; }
	public double Price_Avg { get; set; }
	public double Price_Max { get; set; }
	public double Price_Min { get; set; }

	#endregion

	#region Quantity Aggregation Properties

	public int Quantity_Sum { get; set; }
	public int Quantity_Avg { get; set; }
	public int Quantity_Max { get; set; }
	public int Quantity_Min { get; set; }

	#endregion
}