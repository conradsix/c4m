-- Function to check if setting a parent would create a cycle
CREATE OR REPLACE FUNCTION cms.check_menu_item_cycle()
RETURNS TRIGGER AS $$
DECLARE
    current_id UUID;
BEGIN
    -- If no parent, no cycle possible
    IF NEW.parent_id IS NULL THEN
        RETURN NEW;
    END IF;

    -- Check if we're trying to set parent to self
    IF NEW.id = NEW.parent_id THEN
        RAISE EXCEPTION 'Menu item cannot be its own parent';
    END IF;

    -- Traverse up the hierarchy to check for cycles
    current_id := NEW.parent_id;

    WHILE current_id IS NOT NULL LOOP
        -- If we encounter the item being updated, we have a cycle
        IF current_id = NEW.id THEN
            RAISE EXCEPTION 'Setting parent_id to % would create a cycle', NEW.parent_id;
        END IF;

        -- Move up to the next parent
        SELECT parent_id INTO current_id
        FROM cms.menu_items
        WHERE id = current_id;
    END LOOP;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger for INSERT
CREATE TRIGGER prevent_menu_item_cycle_on_insert
    BEFORE INSERT ON cms.menu_items
    FOR EACH ROW
    EXECUTE FUNCTION cms.check_menu_item_cycle();

-- Trigger for UPDATE
CREATE TRIGGER prevent_menu_item_cycle_on_update
    BEFORE UPDATE OF parent_id ON cms.menu_items
    FOR EACH ROW
    WHEN (OLD.parent_id IS DISTINCT FROM NEW.parent_id)
    EXECUTE FUNCTION cms.check_menu_item_cycle();
