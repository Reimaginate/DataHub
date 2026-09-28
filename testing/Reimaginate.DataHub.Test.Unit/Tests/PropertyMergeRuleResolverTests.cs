using FluentAssertions;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Rules;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class PropertyMergeRuleResolverTests
{
    [Theory]
    [InlineData("Venue.EntityId", "Venue.EntityId")]
    [InlineData("Venue.Address.City.Name", "Venue.Address")]
    [InlineData("Venue.Other.Deep.Value", "Venue")]
    [InlineData("vEnUe.aDdReSs.City", "Venue.Address")]
    [InlineData("Venue", "Venue")]
    [InlineData("VenueOther.EntityId", "*")]
    [InlineData("Other.Venue.EntityId", "*")]
    [InlineData("Venue.AddressOther.City", "Venue")]
    [InlineData("Description", "*")]
    public void Most_specific_rule_wins_regardless_of_configuration_order(string path, string expected)
    {
        var rules = new[] { "*", "Venue", "Venue.Address", "Venue.EntityId" }
            .Select(name => new PropertyMergeRule { PropertyName = name }).ToList();
        foreach (var ordering in new[] { rules, rules.AsEnumerable().Reverse().ToList() })
            PropertyMergeRuleResolver.Resolve(new MergeRule { Rules = ordering }, path)
                .Should().BeSameAs(rules.Single(r => r.PropertyName == expected));
    }

    [Theory]
    [InlineData("Venue.EntityId")]
    [InlineData("Venue.Address.City")]
    [InlineData("Unknown")]
    public void First_configured_duplicate_wins_at_the_selected_specificity(string path)
    {
        var rules = new[] { "*", "*", "Venue", "VENUE", "Venue.EntityId", "VENUE.ENTITYID" }
            .Select(name => new PropertyMergeRule { PropertyName = name }).ToList();
        var expected = path == "Unknown" ? rules[0] : path == "Venue.EntityId" ? rules[4] : rules[2];
        PropertyMergeRuleResolver.Resolve(new MergeRule { Rules = rules }, path).Should().BeSameAs(expected);
    }

    [Fact]
    public void Unmatched_paths_return_no_rule_without_a_wildcard()
    {
        var rules = new MergeRule { Rules = [new() { PropertyName = "Venue" }] };
        PropertyMergeRuleResolver.Resolve(rules, "VenueOther.EntityId").Should().BeNull();
        PropertyMergeRuleResolver.Resolve(new MergeRule { Rules = [] }, "Venue.EntityId").Should().BeNull();
    }
}
