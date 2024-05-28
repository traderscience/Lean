using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Execution;

namespace QuantConnect.Algorithm.Execution
{
    /// <summary>
    /// Immediate Execution with bracket orders
    /// </summary>
    public class ImmediateExecutionWithBracketOrdersModel : ExecutionModel
    {
        private readonly PortfolioTargetCollection _targetsCollection = new PortfolioTargetCollection();

        /// <summary>
        /// 
        /// </summary>
        public ImmediateExecutionWithBracketOrdersModel()
        {

        }


        /// <summary>
        /// Immediately submits orders for the specified portfolio targets.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets to be ordered</param>
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            _targetsCollection.AddRange(targets);
            // for performance we if empty, OrderByMarginImpact and ClearFulfilled are expensive to call
            if (!_targetsCollection.IsEmpty)
            {
                foreach (var target in _targetsCollection.OrderByMarginImpact(algorithm))
                {
                    var security = algorithm.Securities[target.Symbol];

                    // calculate remaining quantity to be ordered
                    var quantity = OrderSizing.GetUnorderedQuantity(algorithm, target, security);
                    if (quantity != 0)
                    {
                        if (security.BuyingPowerModel.AboveMinimumOrderMarginPortfolioPercentage(security, quantity,
                            algorithm.Portfolio, algorithm.Settings.MinimumOrderMarginPortfolioPercentage))
                        {
                            var currentQty = algorithm.Portfolio[security.Symbol].Quantity;
                            OrderIntent intent = OrderIntent.Undefined;
                            if (currentQty == 0)
                                intent = quantity > 0 ? OrderIntent.BTO : OrderIntent.STO;
                            else
                            {
                                if (currentQty > 0)
                                    intent = quantity < 0 ? OrderIntent.STC : OrderIntent.BTO;
                                else
                                    intent = quantity > 0 ? OrderIntent.BTC : OrderIntent.STO;
                            }
                            var orderProps = new OrderProperties()
                            {
                                Intent = intent
                            };

                            // Call handler to create bracket orders here


                            algorithm.MarketOrder(security, quantity, orderProperties: orderProps);
                        }
                        else if (!PortfolioTarget.MinimumOrderMarginPercentageWarningSent.HasValue)
                        {
                            // will trigger the warning if it has not already been sent
                            PortfolioTarget.MinimumOrderMarginPercentageWarningSent = false;
                        }
                    }
                }

                _targetsCollection.ClearFulfilled(algorithm);
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
        }
    }
}
